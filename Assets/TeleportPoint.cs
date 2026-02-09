using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportPoint : MonoBehaviour
{
    [Header("Quick Target")]
    [Tooltip("If targetPoint is empty, auto-find by this scene object name.")]
    public string targetPointName = "lv1-0";

    [Header("Target")]
    [Tooltip("Shop destination transform.")]
    public Transform targetPoint;
    [Tooltip("World-space offset added to target point.")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;
    [Min(0f)] public float cooldown = 0.25f;
    public string playerTag = "Player";

    [Header("Controller Switch In Shop")]
    [Tooltip("True: disable PlayerUnderwaterController and enable playercon in shop mode.")]
    public bool usePccController = false;

    [Header("Spawner Control")]
    [Tooltip("Optional explicit spawners to toggle.")]
    public EnemySpawner[] spawnersToToggle;

    [Header("Pause World In Shop")]
    [Tooltip("Disable everything except the player while inside the shop.")]
    public bool pauseNonPlayerWhileInShop = true;
    public bool freezeNonPlayerRigidbodies = true;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.5f);

    private bool onCooldown;
    private bool inShopMode;
    private Transform player;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private bool cachedUwEnabled;
    private bool cachedPccEnabled;
    private bool hasCachedControllerState;

    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly List<Rigidbody> pausedRigidbodies = new List<Rigidbody>();
    private readonly List<bool> rbKinematicBackup = new List<bool>();
    private readonly List<Vector3> rbVelBackup = new List<Vector3>();
    private readonly List<Vector3> rbAngBackup = new List<Vector3>();
    private static TeleportPoint hotkeyOwner;

    void OnEnable()
    {
        if (hotkeyOwner == null) hotkeyOwner = this;
    }

    void OnDisable()
    {
        if (hotkeyOwner == this) hotkeyOwner = null;
        if (inShopMode)
        {
            ResumeNonPlayerWorld();
            inShopMode = false;
        }
    }

    void Update()
    {
        // Only one TeleportPoint handles global E input to avoid double toggles.
        if (hotkeyOwner != this) return;

        if (onCooldown) return;
        if (!Input.GetKeyDown(interactKey)) return;

        if (IsMenuOpen()) return;
        if (!EnsurePlayer()) return;
        EnsureTargetPoint();
        if (targetPoint == null)
        {
            Debug.LogWarning("TeleportPoint: targetPoint is not set.");
            return;
        }

        StartCoroutine(ToggleShopModeRoutine());
    }

    private IEnumerator ToggleShopModeRoutine()
    {
        onCooldown = true;

        if (!inShopMode)
        {
            EnterShopMode();
        }
        else
        {
            ExitShopMode();
        }

        AudioManager.PlayTeleport();
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }

    private void EnterShopMode()
    {
        if (player == null) return;

        CacheControllerState();
        returnPosition = player.position;
        returnRotation = player.rotation;
        player.position = targetPoint.position + positionOffset;

        ApplyControllerMode(usePccController);
        SetSpawnerEnabled(false);

        if (pauseNonPlayerWhileInShop)
        {
            PauseNonPlayerWorld();
        }

        inShopMode = true;
    }

    private void ExitShopMode()
    {
        if (player == null) return;

        player.position = returnPosition;
        player.rotation = returnRotation;

        RestoreControllerState();
        SetSpawnerEnabled(true);

        if (pauseNonPlayerWhileInShop)
        {
            ResumeNonPlayerWorld();
        }

        inShopMode = false;
    }

    private bool EnsurePlayer()
    {
        if (player != null) return true;
        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return false;
        player = go.transform;
        return true;
    }

    private void EnsureTargetPoint()
    {
        if (targetPoint != null) return;
        if (string.IsNullOrWhiteSpace(targetPointName)) return;
        GameObject go = GameObject.Find(targetPointName);
        if (go != null) targetPoint = go.transform;
    }

    private void CacheControllerState()
    {
        if (player == null || hasCachedControllerState) return;
        var uw = player.GetComponent<PlayerUnderwaterController>();
        var pcc = player.GetComponent<playercon>();
        cachedUwEnabled = uw != null && uw.enabled;
        cachedPccEnabled = pcc != null && pcc.enabled;
        hasCachedControllerState = true;
    }

    private void ApplyControllerMode(bool usePcc)
    {
        if (player == null) return;

        var uw = player.GetComponent<PlayerUnderwaterController>();
        var pcc = player.GetComponent<playercon>();

        if (usePcc)
        {
            if (uw != null) uw.enabled = false;
            if (pcc != null) pcc.enabled = true;
        }
        else
        {
            if (uw != null) uw.enabled = true;
            if (pcc != null) pcc.enabled = false;
        }
    }

    private void RestoreControllerState()
    {
        if (player == null || !hasCachedControllerState) return;
        var uw = player.GetComponent<PlayerUnderwaterController>();
        var pcc = player.GetComponent<playercon>();
        if (uw != null) uw.enabled = cachedUwEnabled;
        if (pcc != null) pcc.enabled = cachedPccEnabled;
        hasCachedControllerState = false;
    }

    private void SetSpawnerEnabled(bool enabled)
    {
        if (spawnersToToggle != null && spawnersToToggle.Length > 0)
        {
            for (int i = 0; i < spawnersToToggle.Length; i++)
            {
                if (spawnersToToggle[i] != null) spawnersToToggle[i].enabled = enabled;
            }
            return;
        }

        var found = Object.FindObjectsOfType<EnemySpawner>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null) found[i].enabled = enabled;
        }
    }

    private void PauseNonPlayerWorld()
    {
        pausedBehaviours.Clear();
        pausedRigidbodies.Clear();
        rbKinematicBackup.Clear();
        rbVelBackup.Clear();
        rbAngBackup.Clear();

        MonoBehaviour[] allBehaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour mb = allBehaviours[i];
            if (mb == null || !mb.enabled) continue;
            if (mb == this) continue;
            if (ShouldKeepActiveInShop(mb)) continue;
            if (IsPlayerObject(mb.gameObject)) continue;
            mb.enabled = false;
            pausedBehaviours.Add(mb);
        }

        if (!freezeNonPlayerRigidbodies) return;

        Rigidbody[] allBodies = Object.FindObjectsOfType<Rigidbody>(true);
        for (int i = 0; i < allBodies.Length; i++)
        {
            Rigidbody rb = allBodies[i];
            if (rb == null) continue;
            if (IsPlayerObject(rb.gameObject)) continue;
            pausedRigidbodies.Add(rb);
            rbKinematicBackup.Add(rb.isKinematic);
            rbVelBackup.Add(rb.linearVelocity);
            rbAngBackup.Add(rb.angularVelocity);
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void ResumeNonPlayerWorld()
    {
        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            if (pausedBehaviours[i] != null) pausedBehaviours[i].enabled = true;
        }
        pausedBehaviours.Clear();

        for (int i = 0; i < pausedRigidbodies.Count; i++)
        {
            Rigidbody rb = pausedRigidbodies[i];
            if (rb == null) continue;
            rb.isKinematic = rbKinematicBackup[i];
            rb.linearVelocity = rbVelBackup[i];
            rb.angularVelocity = rbAngBackup[i];
        }
        pausedRigidbodies.Clear();
        rbKinematicBackup.Clear();
        rbVelBackup.Clear();
        rbAngBackup.Clear();
    }

    private bool IsPlayerObject(GameObject go)
    {
        if (go == null || player == null) return false;
        Transform t = go.transform;
        return t == player || t.IsChildOf(player);
    }

    private bool ShouldKeepActiveInShop(Behaviour behaviour)
    {
        if (behaviour == null) return false;
        GameObject go = behaviour.gameObject;
        if (go == null) return false;
        if (behaviour is CameraDepthFollow) return true;
        System.Type type = behaviour.GetType();
        if (type.Namespace != null && type.Namespace.Contains("Cinemachine")) return true;
        if (go.GetComponent<Camera>() != null) return true;
        if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) return true;
        if (go.GetComponentInParent<Camera>() != null) return true;
        return false;
    }

    private bool IsMenuOpen()
    {
        MainMenuOverlay menu = Object.FindObjectOfType<MainMenuOverlay>(true);
        return menu != null && menu.IsMenuOpen;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || targetPoint == null) return;

        Gizmos.color = gizmoColor;
        Vector3 targetPos = targetPoint.position + positionOffset;
        Gizmos.DrawLine(transform.position, targetPos);
        Gizmos.DrawSphere(targetPos, 0.2f);
    }
}
