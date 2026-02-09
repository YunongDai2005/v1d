using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在玩家范围内为宠物捡取 Exp 物品：优先追最近的 Exp，拾取后让 Exp 排队跟随宠物身后。
/// 达到上限后停止寻找，恢复 pet 脚本对玩家的跟随。
/// </summary>
public class petpick : MonoBehaviour
{
    [Header("引用")]
    public pet petMover;          // 使用已有 pet 脚本的移动速度/跟随逻辑
    public Transform player;      // 默认自动按 Tag 查找

    [Header("搜索设置")]
    public string playerTag = "Player";
    public string expTag = "Exp";
    public float searchRadius = 12f;      // 以玩家为中心的搜索半径
    public float retargetInterval = 0.25f;

    [Header("携带限制")]
    public int maxCarry = 10;
    public Transform followAnchor;        // Exp 跟随锚点（为空则用自身）
    public float followSpacing = 0.6f;    // 队列间距
    [Tooltip("越小越柔和的延迟跟随时间（SmoothDamp 的 smoothTime）。")]
    public float followSmoothTime = 0.25f;
    public float followMaxSpeed = 15f;    // 跟随最大速度

    [Header("收益")]
    public float gainPerExp = 0.1f;       // 每个交付给玩家的 K 增量
    public PlayerUnderwaterController playerCtrl; // 玩家控制脚本，提供 difficultyK（K）

    private readonly List<Transform> _carried = new List<Transform>();
    private readonly Dictionary<Transform, Vector3> _velocities = new Dictionary<Transform, Vector3>();
    private Transform _currentExp;
    private float _retargetTimer;

    void Awake()
    {
        if (!petMover) petMover = GetComponent<pet>();
        if (!followAnchor) followAnchor = transform;
        FindPlayerIfNeeded();
    }

    void Update()
    {
        FindPlayerIfNeeded();
        HandleTargeting();
        UpdateCarriedFollow();
    }

    private void HandleTargeting()
    {
        if (petMover == null) return;

        bool full = _carried.Count >= maxCarry;

        // 达到上限或目标失效时回到玩家
        if (full)
        {
            _currentExp = null;
            petMover.target = player;
            return;
        }

        _retargetTimer -= Time.deltaTime;
        if (_currentExp == null || _retargetTimer <= 0f)
        {
            _retargetTimer = retargetInterval;
            _currentExp = FindNearestExp();
        }

        // 没有可捡则跟随玩家
        petMover.target = _currentExp ? _currentExp : player;
    }

    private Transform FindNearestExp()
    {
        if (player == null) return null;

        Collider[] hits = Physics.OverlapSphere(player.position, searchRadius);
        Transform best = null;
        float bestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (!hit || !hit.CompareTag(expTag)) continue;
            Transform t = hit.transform;
            if (_carried.Contains(t)) continue;

            float dist = Vector3.SqrMagnitude(t.position - transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }
        return best;
    }

    private void UpdateCarriedFollow()
    {
        if (_carried.Count == 0) return;

        Vector3 basePos = followAnchor.position;
        Vector3 backDir = -followAnchor.forward.normalized;
        if (backDir == Vector3.zero) backDir = -Vector3.forward;

        for (int i = 0; i < _carried.Count; i++)
        {
            Transform t = _carried[i];
            if (!t) continue;

            Vector3 targetPos = basePos + backDir * followSpacing * (i + 1);
            if (!_velocities.ContainsKey(t)) _velocities[t] = Vector3.zero;
            Vector3 vel = _velocities[t];
            t.position = Vector3.SmoothDamp(t.position, targetPos, ref vel, followSmoothTime, followMaxSpeed, Time.deltaTime);
            _velocities[t] = vel;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryCashIn(other.transform);
        TryPickup(other.transform);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryCashIn(collision.transform);
        TryPickup(collision.transform);
    }

    private void TryPickup(Transform t)
    {
        if (!t || _carried.Count >= maxCarry) return;
        if (!t.CompareTag(expTag)) return;
        if (_carried.Contains(t)) return;

        _carried.Add(t);
        _currentExp = null;

        // 关闭碰撞和物理，避免干扰
        var rb = t.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
        var col = t.GetComponent<Collider>();
        if (col) col.enabled = false;

        // 如果有原脚本，防止自动销毁
        var expPickup = t.GetComponent<ExpPickup>();
        if (expPickup) expPickup.enabled = false;

        t.SetParent(followAnchor, worldPositionStays: true);
    }

    private void TryCashIn(Transform t)
    {
        if (t == null || !t.CompareTag(playerTag)) return;

        // 确认有 PlayerUnderwaterController 可加 K
        EnsurePlayerCtrlRef(t);

        if (_carried.Count == 0) return;

        int deliveredCount = _carried.Count;
        for (int i = _carried.Count - 1; i >= 0; i--)
        {
            Transform exp = _carried[i];
            if (exp != null)
            {
                Destroy(exp.gameObject);
            }
            _carried.RemoveAt(i);
            _velocities.Remove(exp);

            if (playerCtrl != null)
            {
                playerCtrl.difficultyK += gainPerExp;
            }
        }

        CoinContainerDisplay.AddCoinsGlobal(deliveredCount);
        AudioManager.PlayPickupBurst(deliveredCount);
        _currentExp = null;
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null) return;
        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null) player = go.transform;

        EnsurePlayerCtrlRef(player);
    }

    private void EnsurePlayerCtrlRef(Transform playerTransform)
    {
        if (playerCtrl != null || playerTransform == null) return;
        playerCtrl = playerTransform.GetComponent<PlayerUnderwaterController>();
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(player.position, searchRadius);
    }
}
