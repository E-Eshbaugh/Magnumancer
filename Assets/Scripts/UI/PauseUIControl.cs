using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseUIControl : MonoBehaviour
{
    [Header("Selectors (top→bottom)")]
    public GameObject[] selectors = new GameObject[3];

    [Header("Pause Menu Root")]
    [Tooltip("The root GameObject that you enable/disable for the pause menu")]
    public GameObject pauseMenuUI;

    [Header("Controller")]
    [Tooltip("Controller driving the menu (whoever pressed Start); null = any controller")]
    public Gamepad gamepad;
    public float deadzone = 0.2f;
    public float threshold = 0.6f;

    private int _currentIndex = 0;
    private bool _stickReleased = true;
    private int _openedFrame = -1;

    // Note: this lives on the menu root, which PauseMenScript hides in its Start —
    // so this component's Start only runs the first time the menu opens. Don't hide
    // the menu from here (that made the first Start press show nothing).
    void Start()
    {
        if (selectors.Length != 3)
            Debug.LogError("PauseUIControl requires exactly 3 selectors.");
        UpdateActiveSelector();
    }

    void Update()
    {
        // The Start press that opened the menu isn't a menu input
        if (Time.frameCount == _openedFrame) return;

        // 1) Navigate with either stick or the d-pad
        float y = ReadVertical();
        if (Mathf.Abs(y) < deadzone)
            _stickReleased = true;

        if (_stickReleased && y > threshold)
        {
            _stickReleased = false;
            _currentIndex = Mathf.Max(0, _currentIndex - 1);
            UpdateActiveSelector();
        }
        else if (_stickReleased && y < -threshold)
        {
            _stickReleased = false;
            _currentIndex = Mathf.Min(selectors.Length - 1, _currentIndex + 1);
            UpdateActiveSelector();
        }

        // 2) Confirm with A (buttonSouth)
        if (Pressed(p => p.buttonSouth.wasPressedThisFrame))
            HandleSelection();
        // 3) B resumes any time
        else if (Pressed(p => p.buttonEast.wasPressedThisFrame))
            ClosePauseMenu();
    }

    float ReadVertical()
    {
        if (gamepad != null) return VerticalOf(gamepad);

        float best = 0f;
        foreach (var pad in Gamepad.all)
        {
            float v = VerticalOf(pad);
            if (Mathf.Abs(v) > Mathf.Abs(best)) best = v;
        }
        return best;
    }

    static float VerticalOf(Gamepad pad)
    {
        float y = pad.rightStick.y.ReadValue();
        float l = pad.leftStick.y.ReadValue();
        if (Mathf.Abs(l) > Mathf.Abs(y)) y = l;
        float d = pad.dpad.y.ReadValue();
        if (Mathf.Abs(d) > Mathf.Abs(y)) y = d;
        return y;
    }

    bool Pressed(System.Func<Gamepad, bool> check)
    {
        if (gamepad != null) return check(gamepad);
        foreach (var pad in Gamepad.all)
            if (check(pad)) return true;
        return false;
    }

    private void UpdateActiveSelector()
    {
        for (int i = 0; i < selectors.Length; i++)
            if (selectors[i] != null)
                selectors[i].SetActive(i == _currentIndex);
    }

    private void HandleSelection()
    {
        switch (_currentIndex)
        {
            case 0:
                ClosePauseMenu();
                break;
            case 1:
                // Return to MainMenu scene
                GamePause.Resume(); // unpause before scene load
                SceneManager.LoadScene("MainMenu");
                break;
            case 2:
                // Settings placeholder
                Debug.Log("PauseMenu: Settings selected (not implemented)");
                break;
        }
    }

    /// <summary>
    /// Opens the pause menu, driven by the controller that paused (null = any).
    /// </summary>
    public void OpenPauseMenu(Gamepad presser = null)
    {
        gamepad = presser;
        _openedFrame = Time.frameCount;
        _currentIndex = 0;
        _stickReleased = false; // don't jump selection if a stick is already held
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);
        UpdateActiveSelector();
        GamePause.Pause();
    }

    public void ClosePauseMenu()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        GamePause.Resume();
    }
}
