using UnityEngine;

/// <summary>
/// Local-space drift + spin constrained inside a box volume.
/// </summary>
public class CoinInContainerMotion : MonoBehaviour
{
    private Vector3 _center;
    private Vector3 _halfExtents;
    private Vector3 _velocityLocal;
    private Vector3 _spinAxis;
    private float _spinSpeed;

    public void Initialize(
        Vector3 center,
        Vector3 halfExtents,
        Vector2 moveSpeedRange,
        Vector2 spinSpeedRange)
    {
        _center = center;
        _halfExtents = new Vector3(
            Mathf.Max(0.001f, halfExtents.x),
            Mathf.Max(0.001f, halfExtents.y),
            Mathf.Max(0.001f, halfExtents.z)
        );

        Vector3 dir = Random.insideUnitSphere;
        if (dir.sqrMagnitude < 1e-5f) dir = Vector3.right;
        dir.Normalize();
        float moveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        _velocityLocal = dir * moveSpeed;

        _spinAxis = Random.onUnitSphere;
        if (_spinAxis.sqrMagnitude < 1e-5f) _spinAxis = Vector3.up;
        _spinAxis.Normalize();
        _spinSpeed = Random.Range(spinSpeedRange.x, spinSpeedRange.y);
        if (Random.value < 0.5f) _spinSpeed = -_spinSpeed;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 next = transform.localPosition + _velocityLocal * dt;
        Vector3 rel = next - _center;

        if (Mathf.Abs(rel.x) > _halfExtents.x)
        {
            rel.x = Mathf.Sign(rel.x) * _halfExtents.x;
            _velocityLocal.x *= -1f;
        }
        if (Mathf.Abs(rel.y) > _halfExtents.y)
        {
            rel.y = Mathf.Sign(rel.y) * _halfExtents.y;
            _velocityLocal.y *= -1f;
        }
        if (Mathf.Abs(rel.z) > _halfExtents.z)
        {
            rel.z = Mathf.Sign(rel.z) * _halfExtents.z;
            _velocityLocal.z *= -1f;
        }

        transform.localPosition = _center + rel;
        transform.Rotate(_spinAxis, _spinSpeed * dt, Space.Self);
    }
}
