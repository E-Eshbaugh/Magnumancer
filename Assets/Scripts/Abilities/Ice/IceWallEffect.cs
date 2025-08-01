using UnityEngine;
using System.Collections;

public class IceWallEffect : MonoBehaviour
{
    [Header("Rise Settings")]
    [SerializeField] float riseFromBelow = 3f;
    [SerializeField] float riseSpeed = 10f;
    [SerializeField] float raycastHeight = 5f;
    [SerializeField] LayerMask groundLayers;

    [Header("Health Settings")]
    [SerializeField]
    int maxHealth = 150;
    [SerializeField] GameObject destroyVFX; // optional
    [SerializeField] AudioClip destroySFX; // optional
    [SerializeField] AudioSource audioSource;

    private int currentHealth = 300;
    private float groundY;
    public float riseHeight = 2f;
    public float riseDuration = 0.3f;
    public float meltDuration = 0.5f;

    private Vector3 originalPosition;

    void Awake()
    {
        originalPosition = transform.position;
        transform.position -= Vector3.up * riseHeight;
    }

    public void BeginRise()
    {
        StartCoroutine(Rise());
    }

    public void BeginMelt()
    {
        StartCoroutine(MeltAndDestroy());
    }

    private IEnumerator Rise()
    {
        Vector3 start = transform.position;
        Vector3 end = originalPosition;
        float timer = 0f;

        while (timer < riseDuration)
        {
            float t = timer / riseDuration;
            transform.position = Vector3.Lerp(start, end, t);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
    }

    private IEnumerator MeltAndDestroy()
    {
        Vector3 start = transform.position;
        Vector3 end = start - Vector3.up * riseHeight;
        float timer = 0f;

        while (timer < meltDuration)
        {
            float t = timer / meltDuration;
            transform.position = Vector3.Lerp(start, end, t);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    IEnumerator RiseToGround()
    {
        while (transform.position.y < groundY)
        {
            transform.position += Vector3.up * riseSpeed * Time.deltaTime;
            yield return null;
        }

        Vector3 pos = transform.position;
        pos.y = groundY;
        transform.position = pos;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Optional: flash, shake, ice crack sound
        }
    }

    private void Die()
    {
        if (destroyVFX)
            Instantiate(destroyVFX, transform.position, Quaternion.identity);

        if (destroySFX && audioSource)
            audioSource.PlayOneShot(destroySFX);

        Destroy(gameObject);
    }

}
