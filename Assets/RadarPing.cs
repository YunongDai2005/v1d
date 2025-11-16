using UnityEngine;

public class RadarPing : MonoBehaviour
{
    public float lifetime = 3f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public Color color = new Color(0f, 1f, 0.35f, 1f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _propertyBlock;
    private Renderer[] _renderers;
    private float _age;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        _age = 0f;
        ApplyColor(1f);
    }

    void Update()
    {
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        float normalized = Mathf.Clamp01(_age / lifetime);
        float strength = fadeCurve.Evaluate(normalized);
        ApplyColor(strength);

        if (_age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyColor(float strength)
    {
        if (_renderers == null || _renderers.Length == 0) return;

        Color current = color;
        current.a *= strength;
        Color emission = current;

        foreach (Renderer renderer in _renderers)
        {
            if (!renderer) continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, current);
            _propertyBlock.SetColor(EmissionId, emission);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
