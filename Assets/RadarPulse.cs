using System.Collections.Generic;
using UnityEngine;

public class RadarPulse : MonoBehaviour
{
    [Header("Pulse")]
    public Renderer ren;
    public float speed = 2f;
    public float min = 0f;
    public float max = 5f;

    [Header("Scan")]
    public Transform scanOrigin;
    public LineRenderer scanLine;
    public float scanLineWidth = 0.05f;
    public Color scanLineColor = new Color(0f, 1f, 0.35f, 0.8f);
    public float scanRadius = 25f;
    public float rotationSpeed = 60f;
    public LayerMask scanMask = ~0;
    public string targetTag = "enemy";
    public float hitCooldown = 0.5f;

    [Header("Ping")]
    public GameObject pingPrefab;
    public float pingLifetime = 3f;
    public float pingScale = 0.25f;
    public Color pingColor = new Color(0f, 1f, 0.35f, 1f);
    public AnimationCurve pingFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public string mapLayerName = "map";

    [Header("Audio")]
    public bool playSweepTick = true;
    public float sweepTickInterval = 0.6f;
    public bool playDetectionPing = true;

    private readonly Dictionary<Transform, float> _lastHits = new Dictionary<Transform, float>();
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _propertyBlock;
    private float _scanAngle;
    private int _mapLayerId = -2;
    private Material _scanLineMaterial;
    private float _sweepTickTimer;

    void Awake()
    {
        if (!ren) ren = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        if (!scanOrigin) scanOrigin = transform;
        if (!scanLine)
        {
            scanLine = CreateScanLine();
        }
    }

    void Update()
    {
        AnimatePulse();
        UpdateScanLine();
        UpdateSweepAudio();
        PerformScan();
    }

    private void AnimatePulse()
    {
        if (!ren) return;

        float v = Mathf.Lerp(min, max, (Mathf.Sin(Time.time * speed) + 1f) * 0.5f);
        ren.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(EmissionColorId, Color.green * v);
        ren.SetPropertyBlock(_propertyBlock);
    }

    private void UpdateScanLine()
    {
        if (!scanOrigin) scanOrigin = transform;

        _scanAngle += rotationSpeed * Time.deltaTime;
        Vector3 origin = scanOrigin.position;
        Vector3 direction = Quaternion.AngleAxis(_scanAngle, Vector3.forward) * Vector3.right;

        if (scanLine)
        {
            if (!scanLine.useWorldSpace)
            {
                scanLine.useWorldSpace = true;
            }
            scanLine.positionCount = 2;
            scanLine.startWidth = scanLine.endWidth = scanLineWidth;
            scanLine.SetPosition(0, origin);
            scanLine.SetPosition(1, origin + direction * scanRadius);
        }
    }

    private void PerformScan()
    {
        if (!scanOrigin) scanOrigin = transform;

        Vector3 origin = scanOrigin.position;
        Vector3 direction = Quaternion.AngleAxis(_scanAngle, Vector3.forward) * Vector3.right;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, scanRadius, scanMask, QueryTriggerInteraction.Ignore))
        {
            bool tagMatches = string.IsNullOrEmpty(targetTag) || hit.collider.CompareTag(targetTag);
            if (!tagMatches) return;

            Transform target = hit.collider.transform;
            float lastHit;
            _lastHits.TryGetValue(target, out lastHit);
            if (Time.time - lastHit < hitCooldown) return;

            SpawnPing(hit.point);
            if (playDetectionPing)
            {
                AudioManager.PlayWarning();
            }
            _lastHits[target] = Time.time;
        }
    }

    private void UpdateSweepAudio()
    {
        if (!playSweepTick) return;

        _sweepTickTimer -= Time.deltaTime;
        if (_sweepTickTimer > 0f) return;

        _sweepTickTimer = Mathf.Max(0.05f, sweepTickInterval);
        AudioManager.PlayWarning();
    }

    private LineRenderer CreateScanLine()
    {
        GameObject obj = new GameObject("RadarScanLine");
        obj.transform.SetParent(scanOrigin ? scanOrigin : transform, false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetOrCreateScanLineMaterial();
        line.startWidth = line.endWidth = scanLineWidth;
        line.positionCount = 2;
        int mapLayer = ResolveMapLayer();
        if (mapLayer >= 0)
        {
            SetLayerRecursively(obj, mapLayer);
        }
        return line;
    }

    private Material GetOrCreateScanLineMaterial()
    {
        if (_scanLineMaterial) return _scanLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Unlit/Color");
        _scanLineMaterial = new Material(shader);
        _scanLineMaterial.color = scanLineColor;
        return _scanLineMaterial;
    }

    private void SpawnPing(Vector3 position)
    {
        GameObject pingInstance = pingPrefab ? Instantiate(pingPrefab, position, Quaternion.identity) : CreateFallbackPing(position);
        if (!pingInstance) return;

        pingInstance.transform.localScale = Vector3.one * pingScale;

        int mapLayer = ResolveMapLayer();
        if (mapLayer >= 0)
        {
            SetLayerRecursively(pingInstance, mapLayer);
        }

        RadarPing ping = pingInstance.GetComponent<RadarPing>();
        if (!ping)
        {
            ping = pingInstance.AddComponent<RadarPing>();
        }

        ping.color = pingColor;
        ping.lifetime = pingLifetime;
        ping.fadeCurve = pingFadeCurve;
    }

    private GameObject CreateFallbackPing(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        Collider col = sphere.GetComponent<Collider>();
        if (col)
        {
            Destroy(col);
        }

        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Color");
            if (shader)
            {
                Material mat = new Material(shader);
                mat.color = pingColor;
                renderer.sharedMaterial = mat;
            }
        }

        return sphere;
    }

    private int ResolveMapLayer()
    {
        if (_mapLayerId != -2) return _mapLayerId;

        if (!string.IsNullOrEmpty(mapLayerName))
        {
            _mapLayerId = LayerMask.NameToLayer(mapLayerName);
        }

        if (_mapLayerId == -1)
        {
            _mapLayerId = gameObject.layer;
        }

        return _mapLayerId;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;

        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
