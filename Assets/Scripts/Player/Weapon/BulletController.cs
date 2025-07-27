using UnityEngine;

public class BulletController : MonoBehaviour
{
    public float speed = 20f;
    public Rigidbody2D rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb . linearVelocity = transform.right * speed;
    }

    //bullet colligions
    
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.name != "Bullet(Clone)")
        {
            Destroy(gameObject);
        }
    }
}
