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

    private int currentHealth;
    private float groundY;
    public float riseHeight = 2f;
    public float riseDuration = 0.3f;
    public float meltDuration = 0.5f;

    private Vector3 originalPosition;

    // damage feedback
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Color[] baseColors;
    Coroutine shake;

    public float HealthFraction => maxHealth > 0 ? Mathf.Clamp01(currentHealth / (float)maxHealth) : 0f;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                          : m != null && m.HasProperty("_Color") ? m.color : Color.white;
        }
        currentHealth = maxHealth;
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
        if (currentHealth <= 0) return;
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            ShowDamage();
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(Shudder());
        }
    }

    /// Broken on purpose (too many walls): same as being destroyed
    public void Shatter()
    {
        if (currentHealth <= 0) return;
        currentHealth = 0;
        Die();
    }

    // Dimmer and more see-through the closer it is to breaking
    void ShowDamage()
    {
        float h = HealthFraction;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = Color.Lerp(baseColors[i] * new Color(0.45f, 0.55f, 0.7f, 1f), baseColors[i], h);
            c.a = baseColors[i].a * Mathf.Lerp(0.45f, 1f, h);
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            renderers[i].SetPropertyBlock(block);
        }
    }

    // A quick jolt when hit (doesn't drift: always returns to where it stood)
    IEnumerator Shudder()
    {
        Vector3 home = originalPosition;
        for (float t = 0; t < 0.12f; t += Time.deltaTime)
        {
            transform.position = home + Random.insideUnitSphere * 0.06f * (1f - t / 0.12f);
            yield return null;
        }
        transform.position = home;
        shake = null;
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
