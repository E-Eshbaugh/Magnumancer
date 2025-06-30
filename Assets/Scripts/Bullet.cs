using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 20f;          // units per second
    public float lifetime = 5f;        // seconds until auto-destroy

    private Vector3 direction = Vector3.forward;

    void Start()
    {
        // Schedule auto-destruction
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Move in a straight line
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        // TODO: handle hits (e.g. damage target)
        Destroy(gameObject);
    }

    /// <summary>
    /// Call this right after Instantiate to set the travel direction.
    /// </summary>
    public void Fire(Vector3 dir)
    {
        direction = dir.normalized;
    }
}


