using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExpPickup : MonoBehaviour
{
    [Header("??????")]
    public float addMass = 0.1f;
    public float moveSpeed = 2f;         // ??????
    public float floatAmplitude = 0.2f;  // ???????????
    public float floatFrequency = 1.5f;  // ??????????
    public bool enableMagnet = true;     // Toggle attraction to the player
    public float spinSpeed = 90f;        // Degrees per second for spinning
    public Vector3 spinAxis = Vector3.up;

    private Transform player;
    private Vector3 basePos;
    private float floatPhaseOffset;

    void Start()
    {
        // ????????????
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // ????��???
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        basePos = transform.position;
        floatPhaseOffset = Random.Range(0f, Mathf.PI * 2f);   // Unsynced float

        // ??????
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
        }
    }

    void Update()
    {
        Vector3 target = basePos;

        if (enableMagnet && player != null)
        {
            target = player.position;
        }

        Vector3 floatOffset = Vector3.up * Mathf.Sin(Time.time * floatFrequency + floatPhaseOffset) * floatAmplitude;
        Vector3 nextPosition = Vector3.Lerp(transform.position, target + floatOffset, Time.deltaTime * moveSpeed);

        transform.position = nextPosition;
        ApplySpin();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerUnderwaterController playerCtrl = other.GetComponent<PlayerUnderwaterController>();
            if (playerCtrl != null)
            {
                playerCtrl.totalMass += addMass;
                Debug.Log($"? ???????????????????? {playerCtrl.totalMass}");
            }

            CoinContainerDisplay.AddCoinsGlobal(1);
            AudioManager.PlayPickup();
            Destroy(gameObject);
        }
    }

    private void ApplySpin()
    {
        if (spinSpeed == 0f || spinAxis == Vector3.zero)
        {
            return;
        }

        transform.Rotate(spinAxis.normalized * spinSpeed * Time.deltaTime, Space.World);
    }
}
