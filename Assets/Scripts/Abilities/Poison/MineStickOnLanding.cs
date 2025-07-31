using UnityEngine;

public class MineStickOnLanding : MonoBehaviour
{
    [Header("Landing")]
    public LayerMask groundMask;
    public float alignOffset = 0.01f;

    private Rigidbody rb;
    private bool hasLanded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;

        // Only react to ground-layer collisions
        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0) return;

        ContactPoint contact = collision.contacts[0];
        StickToSurface(contact.point, contact.normal);
    }

    void StickToSurface(Vector3 hitPoint, Vector3 surfaceNormal)
    {
        hasLanded = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        transform.position = hitPoint + surfaceNormal * alignOffset;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
    }
}
