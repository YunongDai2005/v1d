using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class MenuAutoDodge : MonoBehaviour
{
    [Header("Auto Dodge")]
    public string enemyTag = "enemy";
    public float searchRadius = 30f;
    public float moveSpeed = 5.5f;
    public float dashSpeed = 10.5f;
    public float dashInterval = 0.8f;
    public float retargetInterval = 0.12f;
    public float randomLateralWeight = 0.5f;
    public bool simulatePlayerInput = true;
    [Header("Escape Strategy")]
    public int sampleDirections = 16;
    public float surroundDangerRadius = 6f;
    public int gapDisableNearbyEnemyCount = 3;
    public float minDirectionHoldTime = 0.2f;
    public float directionSmoothSpeed = 10f;
    [Header("Input Cadence")]
    public bool useInputCadence = true;
    public Vector2 moveInputDurationRange = new Vector2(0.45f, 1.1f);
    public Vector2 gapInputDurationRange = new Vector2(0.08f, 0.22f);

    private Rigidbody _rb;
    private PlayerUnderwaterController _underwaterController;
    private readonly List<Transform> _nearbyEnemies = new List<Transform>();
    private float _dashTimer;
    private float _retargetTimer;
    private bool _autoDodgeEnabled;
    private bool _isInputGap;
    private float _inputPhaseTimer;
    private Vector3 _smoothedDir = Vector3.right;
    private Vector3 _lockedDir = Vector3.right;
    private float _directionHoldTimer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _underwaterController = GetComponent<PlayerUnderwaterController>();
        if (_underwaterController == null)
            _underwaterController = GetComponentInChildren<PlayerUnderwaterController>(true);
        if (_underwaterController == null)
            _underwaterController = GetComponentInParent<PlayerUnderwaterController>();
    }

    private void OnDisable()
    {
        if (_rb != null)
        {
            Vector3 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(0f, 0f, v.z);
        }

        if (_underwaterController != null)
        {
            _underwaterController.SetExternalInput(Vector2.zero, false);
            _underwaterController.SetUseExternalInput(false);
        }
    }

    public void SetAutoDodgeEnabled(bool enabled)
    {
        _autoDodgeEnabled = enabled;
        if (_rb == null) return;

        if (_autoDodgeEnabled)
        {
            _isInputGap = false;
            _inputPhaseTimer = RandomInRange(moveInputDurationRange, 0.65f);
            _directionHoldTimer = 0f;
            _lockedDir = Vector3.right;
            _smoothedDir = Vector3.right;

            if (_underwaterController == null)
                _underwaterController = GetComponent<PlayerUnderwaterController>();
            if (_underwaterController == null)
                _underwaterController = GetComponentInChildren<PlayerUnderwaterController>(true);
            if (_underwaterController == null)
                _underwaterController = GetComponentInParent<PlayerUnderwaterController>();

            if (simulatePlayerInput && _underwaterController != null)
            {
                _underwaterController.SetUseExternalInput(true);
            }

            if (_rb.isKinematic)
            {
                _rb.isKinematic = false;
            }
        }
        else
        {
            Vector3 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(0f, 0f, v.z);
            if (_underwaterController != null)
            {
                _underwaterController.SetExternalInput(Vector2.zero, false);
                _underwaterController.SetUseExternalInput(false);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_autoDodgeEnabled || _rb == null)
            return;

        _retargetTimer -= Time.fixedDeltaTime;
        if (_retargetTimer <= 0f || _nearbyEnemies.Count == 0)
        {
            _retargetTimer = retargetInterval;
            RefreshThreats();
        }

        if (useInputCadence)
        {
            TickInputCadence(Time.fixedDeltaTime);
        }

        Vector3 desiredDir = GetEvadeDirection();
        Vector3 moveDir = GetStableDirection(desiredDir, Time.fixedDeltaTime);

        bool forceMove = ShouldForceMove();
        if (_isInputGap && !forceMove)
        {
            if (simulatePlayerInput && _underwaterController != null)
            {
                _underwaterController.SetExternalInput(Vector2.zero, false);
            }
            else
            {
                _rb.linearVelocity = Vector3.zero;
            }
            return;
        }

        _dashTimer -= Time.fixedDeltaTime;

        float speed = moveSpeed;
        bool triggerDash = false;
        if (_dashTimer <= 0f)
        {
            _dashTimer = Mathf.Max(0.1f, dashInterval);
            speed = dashSpeed;
            triggerDash = true;
            AudioManager.PlayDash();
        }

        if (simulatePlayerInput && _underwaterController != null)
        {
            // Input vector controls movement; dash uses pulse signal.
            Vector2 moveInput = new Vector2(moveDir.x, moveDir.y);
            _underwaterController.SetExternalInput(moveInput, triggerDash);
            return;
        }

        Vector3 targetVel = moveDir * speed;
        _rb.linearVelocity = new Vector3(targetVel.x, targetVel.y, 0f);
    }

    private void TickInputCadence(float dt)
    {
        _inputPhaseTimer -= dt;
        if (_inputPhaseTimer > 0f) return;

        _isInputGap = !_isInputGap;
        _inputPhaseTimer = _isInputGap
            ? RandomInRange(gapInputDurationRange, 0.15f)
            : RandomInRange(moveInputDurationRange, 0.65f);
    }

    private static float RandomInRange(Vector2 range, float fallback)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        if (max <= 0f) return fallback;
        min = Mathf.Max(0.01f, min);
        max = Mathf.Max(min, max);
        return Random.Range(min, max);
    }

    private void RefreshThreats()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        _nearbyEnemies.Clear();
        if (enemies == null || enemies.Length == 0) return;

        Vector3 p = transform.position;
        float maxSqr = searchRadius * searchRadius;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject e = enemies[i];
            if (e == null) continue;

            Vector3 d = e.transform.position - p;
            float sqr = d.sqrMagnitude;
            if (sqr <= maxSqr && IsValidEnemy(e))
            {
                _nearbyEnemies.Add(e.transform);
            }
        }
    }

    private static bool IsValidEnemy(GameObject go)
    {
        if (go == null) return false;
        return go.GetComponent<EnemyAI>() != null || go.GetComponent<EnemyHealth>() != null;
    }

    private Vector3 GetEvadeDirection()
    {
        if (_nearbyEnemies.Count == 0)
        {
            Vector3 away = Random.insideUnitSphere;
            away.z = 0f;
            if (away.sqrMagnitude <= 1e-5f) away = Vector3.right;
            return away.normalized;
        }

        Vector3 p = transform.position;
        Vector3 weightedAway = Vector3.zero;

        for (int i = _nearbyEnemies.Count - 1; i >= 0; i--)
        {
            Transform t = _nearbyEnemies[i];
            if (t == null)
            {
                _nearbyEnemies.RemoveAt(i);
                continue;
            }

            Vector3 d = p - t.position;
            d.z = 0f;
            float sqr = Mathf.Max(0.05f, d.sqrMagnitude);
            weightedAway += d.normalized / sqr;
        }

        if (weightedAway.sqrMagnitude <= 1e-6f)
            weightedAway = Vector3.right;
        weightedAway.Normalize();

        Vector3 bestDir = FindLowestDangerDirection(weightedAway);
        Vector3 side = Vector3.Cross(Vector3.forward, bestDir).normalized;
        float sideSign = Random.value < 0.5f ? -1f : 1f;
        Vector3 mixed = (bestDir + side * sideSign * Mathf.Clamp01(randomLateralWeight) * 0.35f).normalized;
        return mixed.sqrMagnitude > 1e-6f ? mixed : bestDir;
    }

    private Vector3 FindLowestDangerDirection(Vector3 fallback)
    {
        int samples = Mathf.Max(8, sampleDirections);
        Vector3 p = transform.position;
        Vector3 best = fallback;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < samples; i++)
        {
            float ang = (Mathf.PI * 2f) * (i / (float)samples);
            Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            float score = 0f;

            for (int k = 0; k < _nearbyEnemies.Count; k++)
            {
                Transform t = _nearbyEnemies[k];
                if (t == null) continue;

                Vector3 toEnemy = t.position - p;
                toEnemy.z = 0f;
                float dist = Mathf.Max(0.2f, toEnemy.magnitude);
                float toward = Mathf.Max(0f, Vector3.Dot(dir, toEnemy / dist));
                score += toward / dist;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = dir;
            }
        }

        return best.normalized;
    }

    private bool ShouldForceMove()
    {
        int closeCount = 0;
        float dangerSqr = surroundDangerRadius * surroundDangerRadius;
        Vector3 p = transform.position;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            Transform t = _nearbyEnemies[i];
            if (t == null) continue;
            Vector3 d = t.position - p;
            d.z = 0f;
            if (d.sqrMagnitude <= dangerSqr)
            {
                closeCount++;
                if (closeCount >= Mathf.Max(1, gapDisableNearbyEnemyCount))
                    return true;
            }
        }

        return false;
    }

    private Vector3 GetStableDirection(Vector3 desiredDir, float dt)
    {
        if (desiredDir.sqrMagnitude <= 1e-6f)
            desiredDir = _lockedDir.sqrMagnitude > 1e-6f ? _lockedDir : Vector3.right;

        desiredDir.Normalize();

        _directionHoldTimer -= dt;
        bool shouldSwitch = _directionHoldTimer <= 0f
            || Vector3.Dot(_lockedDir, desiredDir) < 0.35f;

        if (shouldSwitch)
        {
            _lockedDir = desiredDir;
            _directionHoldTimer = Mathf.Max(0.02f, minDirectionHoldTime);
        }

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionSmoothSpeed) * dt);
        _smoothedDir = Vector3.Lerp(_smoothedDir, _lockedDir, t);
        _smoothedDir.z = 0f;
        if (_smoothedDir.sqrMagnitude <= 1e-6f)
            _smoothedDir = _lockedDir;
        return _smoothedDir.normalized;
    }
}
