using UnityEngine;

[ExecuteAlways]
public class bocon : MonoBehaviour
{
    private const int LightCount = 5;

    [System.Serializable]
    private class DepthLight
    {
        public Light light;
        public bool overridePosition;
        public Vector3 localPosition;

        public void ApplyPosition(Transform root)
        {
            if (!overridePosition || light == null) return;
            light.transform.position = root.TransformPoint(localPosition);
        }

        public void ApplyVisuals(bool on, float onIntensity)
        {
            if (light == null) return;
            if (on)
            {
                light.intensity = onIntensity;
                light.enabled = true;
            }
            else
            {
                light.intensity = 0f;
                light.enabled = false;
            }
        }
    }

    [Header("Tracking")]
    [SerializeField] private PlayerUnderwaterController underwaterController;
    [SerializeField] private Transform diver;
    [SerializeField] private float targetDepth = -30f;

    [Header("Display")]
    [SerializeField, Min(0f)] private float balanceDeadband = 0.25f;
    [SerializeField, Min(0f)] private float moderateBand = 20f;
    [SerializeField, Min(0f)] private float extremeBand = 35f;
    [SerializeField, Min(0f)] private float onIntensity = 400000f;
    [SerializeField] private DepthLight[] depthLights = new DepthLight[LightCount];

    private void Reset()
    {
        EnsureLightSlots();

        float spacing = 0.5f;
        for (int i = 0; i < depthLights.Length; i++)
        {
            depthLights[i].overridePosition = true;
            depthLights[i].localPosition = new Vector3(i * spacing, 0f, 0f);
        }
    }

    private void OnValidate()
    {
        EnsureLightSlots();
    }

    private void Update()
    {
        if (depthLights == null)
        {
            return;
        }

        float offset = 0f;
        bool hasSource = false;

        if (underwaterController != null)
        {
            offset = underwaterController.deltaH;
            hasSource = true;
        }
        else if (diver != null)
        {
            offset = diver.position.y - targetDepth;
            hasSource = true;
        }

        if (!hasSource)
        {
            return;
        }

        UpdateLights(offset);
    }

    private void UpdateLights(float offset)
    {
        if (depthLights.Length == 0)
        {
            return;
        }

        ValidateBands();

        int centerIndex = depthLights.Length / 2;
        int activeIndex = centerIndex;

        float absOffset = Mathf.Abs(offset);
        if (absOffset <= balanceDeadband)
        {
            activeIndex = centerIndex;
        }
        else if (offset >= extremeBand)
        {
            activeIndex = 0; // highest
        }
        else if (offset >= moderateBand)
        {
            activeIndex = 1; // second highest
        }
        else if (offset <= -extremeBand)
        {
            activeIndex = depthLights.Length - 1; // lowest
        }
        else if (offset <= -moderateBand)
        {
            activeIndex = depthLights.Length - 2; // second lowest
        }
        else
        {
            activeIndex = centerIndex;
        }

        for (int i = 0; i < depthLights.Length; i++)
        {
            depthLights[i].ApplyPosition(transform);
            bool shouldBeOn = i == activeIndex;
            depthLights[i].ApplyVisuals(shouldBeOn, onIntensity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (depthLights == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < depthLights.Length; i++)
        {
            Vector3 position = transform.position;
            if (depthLights[i].overridePosition)
            {
                position = transform.TransformPoint(depthLights[i].localPosition);
            }
            else if (depthLights[i].light != null)
            {
                position = depthLights[i].light.transform.position;
            }

            Gizmos.DrawWireSphere(position, 0.1f);
        }
    }

    private void EnsureLightSlots()
    {
        if (depthLights != null && depthLights.Length == LightCount) return;

        DepthLight[] newLights = new DepthLight[LightCount];
        if (depthLights != null)
        {
            for (int i = 0; i < Mathf.Min(depthLights.Length, LightCount); i++)
            {
                newLights[i] = depthLights[i];
            }
        }

        depthLights = newLights;
    }

    private void ValidateBands()
    {
        if (extremeBand < moderateBand)
        {
            extremeBand = moderateBand;
        }
    }
}
