using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DigitalCountUpTimer : MonoBehaviour
{
    [Header("Assign is optional — will auto-find child Text")]
    [SerializeField] private Text label;

    [Header("Behavior")]
    [SerializeField] private bool autoStart = true;
    [Tooltip("Use unscaled time so the timer keeps running while the game is paused.")]
    [SerializeField] private bool useUnscaledTime = false;
    [Tooltip("Show HH:MM:SS once you reach >= 1 hour.")]
    [SerializeField] private bool showHoursWhenNeeded = true;

    private Coroutine _loop;
    private int _totalSeconds;   // starts at 0 -> "00:00"
    private float _accum;

    private void Awake()
    {
        if (!label) label = GetComponentInChildren<Text>(true);
    }

    private void OnEnable()
    {
        if (!label)
        {
            Debug.LogWarning($"[{nameof(DigitalCountUpTimer)}] No Text found under {name}.");
            return;
        }

        label.supportRichText = true;  // harmless; allows color/bold if you ever want it
        _totalSeconds = 0;
        _accum = 0f;
        UpdateLabel();

        if (autoStart) StartTimer();
    }

    private void OnDisable()
    {
        StopTimer();
    }

    // --- Public controls ---
    public void StartTimer()
    {
        if (!label || _loop != null) return;
        _loop = StartCoroutine(TickLoop());
    }

    public void StopTimer()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    public void ResetTimer(bool restart = false)
    {
        _totalSeconds = 0;
        _accum = 0f;
        UpdateLabel();
        if (restart)
        {
            StopTimer();
            StartTimer();
        }
    }

    public int CurrentSeconds => _totalSeconds;

    // Optional: set a specific time (e.g., resuming a run)
    public void SetSeconds(int seconds)
    {
        _totalSeconds = Mathf.Max(0, seconds);
        _accum = 0f;
        UpdateLabel();
    }

    // --- Internals ---
    private IEnumerator TickLoop()
    {
        while (true)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _accum += dt;

            if (_accum >= 1f)
            {
                int whole = (int)_accum;   // handles rare long frames without drift
                _accum -= whole;
                _totalSeconds += whole;
                UpdateLabel();
            }
            yield return null;
        }
    }

    private void UpdateLabel()
    {
        if (!label) return;

        if (showHoursWhenNeeded && _totalSeconds >= 3600)
        {
            int h =  _totalSeconds / 3600;
            int m = (_totalSeconds / 60) % 60;
            int s =  _totalSeconds % 60;
            label.text = $"{h:00}:{m:00}:{s:00}";
        }
        else
        {
            int m = _totalSeconds / 60;
            int s = _totalSeconds % 60;
            label.text = $"{m:00}:{s:00}";
        }
    }

    // Handy editor test hooks
    [ContextMenu("Start Timer")]
    private void CM_Start() => StartTimer();

    [ContextMenu("Stop Timer")]
    private void CM_Stop() => StopTimer();

    [ContextMenu("Reset Timer")]
    private void CM_Reset() => ResetTimer(false);
}
