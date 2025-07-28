using UnityEngine;

public class LavaTrailDropper : MonoBehaviour
{
    [SerializeField] float fallSpeed = 8f;
    [SerializeField] float groundCheckDistance = 0.1f;
    [SerializeField] float offsetY = 0.02f;
    [SerializeField] LayerMask groundMask;

    private bool hasLanded = false;

    void Update()
    {
        if (hasLanded) return;

        // Raycast to check if we're close to the ground
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
        {
            // Snap to surface + offset
            transform.position = hit.point + Vector3.up * offsetY;
            hasLanded = true;
            return;
        }

        // Move downward
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }
}
