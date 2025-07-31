using UnityEngine;

public class MineButtonFlasher : MonoBehaviour
{
    [Header("Flashing Settings")]
    [SerializeField] float flashSpeed = 2f;     // Speed of alpha pulsing
    [SerializeField] float minAlpha = 0.2f;     // Minimum transparency
    [SerializeField] float maxAlpha = 1f;       // Maximum visibility

    private Material materialInstance;
    private Color originalColor;
    private float alpha;
    private float direction = -1f;

    void Start()
    {
        // Get the material instance
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("[MineButtonFlasher] No Renderer found on object.");
            enabled = false;
            return;
        }

        // Create a unique instance so we don't affect other objects using the same material
        materialInstance = renderer.material;
        originalColor = materialInstance.color;
        alpha = maxAlpha;
    }

    void Update()
    {
        // Pulse the alpha
        alpha += direction * flashSpeed * Time.deltaTime;

        if (alpha <= minAlpha)
        {
            alpha = minAlpha;
            direction = 1f;
        }
        else if (alpha >= maxAlpha)
        {
            alpha = maxAlpha;
            direction = -1f;
        }

        // Apply the alpha to the material color
        Color newColor = originalColor;
        newColor.a = alpha;
        materialInstance.color = newColor;
    }
}
