using UnityEngine;

public class ProjectileMover : MonoBehaviour
{
    public float speed = 30f;
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime); // auto-cleanup
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
