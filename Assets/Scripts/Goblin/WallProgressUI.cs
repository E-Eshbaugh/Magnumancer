using UnityEngine;
using UnityEngine.UI;

public class SingleBarHealthUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The colored image whose width we resize.")]
    public Image barImage;

    [Tooltip("Optional container rect. If empty, we'll use barImage's current width as 'full' width.")]
    public RectTransform container;

    [Header("Behavior")]
    [Tooltip("Smoothly lerp toward the new width.")]
    public bool smooth = true;
    public float lerpSpeed = 8f;

    [Tooltip("Minimum width so there's always a visible sliver when HP > 0.")]
    public float minWidth = 4f;

    private RectTransform _barRect;
    private float _fullWidth;     // width when health is 100%
    private float _targetWidth;   // where we want to go
    private float _currentWidth;  // where we are now (for smoothing)

    void Awake()
    {
        if (barImage == null)
        {
            Debug.LogError("[SingleBarHealthUI] barImage is not assigned.");
            enabled = false;
            return;
        }

        _barRect = barImage.rectTransform;

        // Determine "full" width
        if (container != null)
        {
            _fullWidth = container.rect.width;
        }
        else
        {
            // Use current size of the bar as full width
            _fullWidth = _barRect.rect.width;
        }

        // Start full
        _currentWidth = _targetWidth = _fullWidth;
        SetWidthImmediate(_fullWidth);
    }

    void Update()
    {
        if (!smooth)
            return;

        // Smoothly approach target width
        if (Mathf.Abs(_currentWidth - _targetWidth) > 0.01f)
        {
            _currentWidth = Mathf.Lerp(_currentWidth, _targetWidth, Time.deltaTime * Mathf.Max(0.01f, lerpSpeed));
            SetWidthImmediate(_currentWidth);
        }
    }

    /// <summary>
    /// Call this from DestructibleWall after damage:
    /// ui.ReflectWall(this);
    /// </summary>
    public void ReflectWall(DestructibleWall wall)
    {
        if (wall == null || wall.maxHealth <= 0f) return;

        float normalized = Mathf.Clamp01(wall.CurrentHealth01); // assumes property exists on wall
        SetFill(normalized);
    }

    /// <summary>
    /// Drive directly via values (current, max).
    /// </summary>
    public void ReflectValues(float current, float max)
    {
        if (max <= 0f) return;
        SetFill(Mathf.Clamp01(current / max));
    }

    /// <summary>
    /// Instantly fill to 100% (use at round start).
    /// </summary>
    public void ResetFull()
    {
        _targetWidth = _fullWidth;
        _currentWidth = _fullWidth;
        SetWidthImmediate(_fullWidth);
    }

    /// <summary>
    /// Set the bar to a normalized fill [0..1].
    /// </summary>
    private void SetFill(float normalized01)
    {
        float w = Mathf.Lerp(minWidth, _fullWidth, normalized01);
        _targetWidth = w;
        if (!smooth)
        {
            _currentWidth = w;
            SetWidthImmediate(w);
        }
    }

    private void SetWidthImmediate(float width)
    {
        var size = _barRect.sizeDelta;
        size.x = width;
        _barRect.sizeDelta = size;
    }
}
