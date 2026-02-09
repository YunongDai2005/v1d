using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fades out a ghost instance over time using MaterialPropertyBlock color override.
/// Works best with transparent-capable materials on the ghost prefab.
/// </summary>
public class DashGhostAfterimage : MonoBehaviour
{
    [Header("Force Ghost Look")]
    public bool forceOverrideMaterial = true;
    public bool disableShadows = true;
    [Range(0f, 3f)] public float emissionStrength = 1.1f;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _age;
    private float _lifetime;
    private Color _startColor;
    private Color _endColor;
    private bool _fadeAlphaOnly;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int SurfaceTypeId = Shader.PropertyToID("_SurfaceType");
    private static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int CullModeId = Shader.PropertyToID("_CullMode");
    private static readonly int CullId = Shader.PropertyToID("_Cull");

    private Material _ghostMat;

    public void Initialize(float lifetime, Color startColor, Color endColor)
    {
        _lifetime = Mathf.Max(0.01f, lifetime);
        _startColor = startColor;
        _endColor = endColor;
        _fadeAlphaOnly = false;
        _age = 0f;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        SetupGhostRendering();
        StripGameplayBehaviours();

        // Keep ghosts purely visual.
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        // Ensure color transition starts immediately.
        ApplyColor(_startColor);
    }

    public void InitializeFixedColor(float lifetime, Color fixedColor)
    {
        _lifetime = Mathf.Max(0.01f, lifetime);
        _startColor = fixedColor;
        _endColor = fixedColor;
        _fadeAlphaOnly = true;
        _age = 0f;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        SetupGhostRendering();
        StripGameplayBehaviours();

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;

        ApplyColor(_startColor);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / _lifetime);
        Color c;
        if (_fadeAlphaOnly)
        {
            c = _startColor;
            c.a = Mathf.Lerp(_startColor.a, 0f, t);
        }
        else
        {
            c = Color.Lerp(_startColor, _endColor, t);
        }
        ApplyColor(c);

        if (_age >= _lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyColor(Color c)
    {
        if (_renderers == null) return;
        Color emission = c * emissionStrength;

        if (forceOverrideMaterial && _ghostMat != null)
        {
            ApplyToMaterial(_ghostMat, c, emission);
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(UnlitColorId, c);
            _mpb.SetColor(EmissionColorId, emission);
            _mpb.SetColor(EmissiveColorId, emission);
            r.SetPropertyBlock(_mpb);

            // Fallback for shaders/materials that ignore property blocks.
            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;
                ApplyToMaterial(mat, c, emission);
            }
        }
    }

    private void SetupGhostRendering()
    {
        if (_renderers == null) return;

        if (forceOverrideMaterial)
        {
            _ghostMat = BuildGhostMaterial();
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            if (disableShadows)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            if (forceOverrideMaterial && _ghostMat != null)
            {
                int slotCount = Mathf.Max(1, r.sharedMaterials != null ? r.sharedMaterials.Length : 1);
                Material[] mats = new Material[slotCount];
                for (int m = 0; m < slotCount; m++) mats[m] = _ghostMat;
                r.materials = mats;
            }
        }
    }

    private void StripGameplayBehaviours()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null || mb == this) continue;
            mb.enabled = false;
        }
    }

    private Material BuildGhostMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.enableInstancing = true;
        mat.renderQueue = (int)RenderQueue.Transparent;

        if (mat.HasProperty(SurfaceTypeId)) mat.SetFloat(SurfaceTypeId, 1f);
        if (mat.HasProperty(BlendModeId)) mat.SetFloat(BlendModeId, 0f);
        if (mat.HasProperty(ZWriteId)) mat.SetFloat(ZWriteId, 0f);
        if (mat.HasProperty(SrcBlendId)) mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        if (mat.HasProperty(DstBlendId)) mat.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty(CullModeId)) mat.SetFloat(CullModeId, 0f);
        if (mat.HasProperty(CullId)) mat.SetFloat(CullId, 0f);
        return mat;
    }

    private void ApplyToMaterial(Material mat, Color c, Color emission)
    {
        if (mat == null) return;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
        if (mat.HasProperty(UnlitColorId)) mat.SetColor(UnlitColorId, c);
        if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, emission);
        if (mat.HasProperty(EmissiveColorId)) mat.SetColor(EmissiveColorId, emission);
    }

    private void OnDestroy()
    {
        if (_ghostMat != null)
        {
            Destroy(_ghostMat);
            _ghostMat = null;
        }
    }
}
