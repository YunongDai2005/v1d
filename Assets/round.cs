using UnityEngine;

public class round : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 45f; // Degrees per second around the Y axis

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.up * (rotationSpeed * Time.deltaTime));
    }
}
