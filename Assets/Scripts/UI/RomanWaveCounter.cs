using System.Text;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RomanWaveCounter : MonoBehaviour
{
    [SerializeField] private Text label; // can be left empty; we auto-find child
    [Header("Display")]
    [SerializeField] private string prefix = "WAVE ";
    [SerializeField] private bool showArabicAfterThreshold = true;
    [SerializeField] private int arabicAppendThreshold = 100;
    [SerializeField] private bool allowVeryLargeRomans = true;
    [SerializeField] private int startingWave = 1;

    [Header("Pop Animation")]
    [SerializeField] private bool animateOnIncrease = true;
    [SerializeField] private float popDuration = 0.14f;
    [SerializeField] private float popScale = 1.35f;

    private int _currentWave;
    private RectTransform _rt;              // child text RectTransform
    private Vector3 _baseScale;             // cached starting scale
    private Vector3 _basePos3D;             // cached starting anchoredPosition3D

    private void Awake()
    {
        if (!label) label = GetComponentInChildren<Text>(true);
        _rt = label ? label.rectTransform : null;
    }

    private void OnEnable()
    {
        if (!label)
        {
            Debug.LogWarning($"[{nameof(RomanWaveCounter)}] No Text found/assigned on {name}.");
            return;
        }

        // Cache starting transform so we never disturb your formatting
        _baseScale = _rt.localScale;
        _basePos3D = _rt.anchoredPosition3D;

        label.supportRichText = true; // safe even if unused
        SetWave(startingWave);        // init display
    }

    public void SetWave(int wave)
    {
        if (!label) return;

        wave = Mathf.Max(0, wave);
        bool increased = wave > _currentWave;
        _currentWave = wave;
        if (increased && wave > 0 && Application.isPlaying) Sfx.Play(SfxId.WaveStart);   // drums: here they come

        string roman = ToRoman(wave, allowVeryLargeRomans);
        string tail = (showArabicAfterThreshold && wave >= arabicAppendThreshold) ? $" ({wave})" : string.Empty;
        label.text = $"{prefix}{roman}{tail}";

        if (animateOnIncrease && increased)
        {
            StopAllCoroutines();
            // Ensure we start from the exact cached transform
            _rt.localScale = _baseScale;
            _rt.anchoredPosition3D = _basePos3D;
            StartCoroutine(PopRelative(_rt, popDuration, popScale));
        }
        else
        {
            // Always enforce your original formatting if anything else tried to move it
            _rt.localScale = _baseScale;
            _rt.anchoredPosition3D = _basePos3D;
        }
    }

    public void Increment() => SetWave(_currentWave + 1);

    // Standard subtractive Roman numerals
    public static string ToRoman(int number, bool allowLarge)
    {
        if (number <= 0) return "N"; // or "-" if you prefer
        if (!allowLarge && number > 3999) number = 3999;

        int[] vals    = {1000,900,500,400,100,90,50,40,10,9,5,4,1};
        string[] syms = {"M","CM","D","CD","C","XC","L","XL","X","IX","V","IV","I"};

        var sb = new StringBuilder(16);
        int n = number;
        for (int i = 0; i < vals.Length; i++)
            while (n >= vals[i]) { n -= vals[i]; sb.Append(syms[i]); }
        return sb.ToString();
    }

    private System.Collections.IEnumerator PopRelative(RectTransform rt, float duration, float peakMul)
    {
        // Animate strictly relative to the cached base transform
        Vector3 startScale = _baseScale;
        Vector3 peakScaleV = _baseScale * peakMul;
        Vector3 startPos   = _basePos3D; // keep fixed

        float half = duration * 0.5f, t = 0f;
        // Up
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = t / half;
            rt.localScale = Vector3.Lerp(startScale, peakScaleV, a);
            rt.anchoredPosition3D = startPos; // enforce exact position
            yield return null;
        }
        // Down
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = t / half;
            rt.localScale = Vector3.Lerp(peakScaleV, startScale, a);
            rt.anchoredPosition3D = startPos;
            yield return null;
        }
        // Hard restore to ensure no drift
        rt.localScale = _baseScale;
        rt.anchoredPosition3D = _basePos3D;
    }

    // If you ever re-position/resize the text at runtime and want to adopt that as new "base",
    // call this to re-cache.
    [ContextMenu("Re-cache Base Transform")]
    private void RecacheBase()
    {
        if (!_rt) return;
        _baseScale = _rt.localScale;
        _basePos3D = _rt.anchoredPosition3D;
    }
}
