using UnityEngine;

public class HealCircleOutter : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = -45f;

    void Update()
    {
        // Spin around the local Y-axis — which is flat if object was rotated 90° on X
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}
