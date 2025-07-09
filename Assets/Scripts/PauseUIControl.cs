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
    public Gamepad gamepad;
    public float deadzone = 0.2f;
    public float threshold = 0.6f;

    private int _currentIndex = 0;
    private bool _stickReleased = true;
    private bool _isPaused = false;

    void Start()
    {
        if (selectors.Length != 3)
            Debug.LogError("PauseUIControl requires exactly 3 selectors.");
        // Ensure pause menu is hidden at start
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        UpdateActiveSelector();
    }

    void Update()
    {
        if (gamepad == null)
            gamepad = Gamepad.current;
        if (gamepad == null)
            return;

        // 1) Navigate with right stick
        float y = gamepad.rightStick.y.ReadValue();
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
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            HandleSelection();
        }

        // 3) Also allow B to unpause any time
        if (gamepad.buttonEast.wasPressedThisFrame)
            HandleSelectionCancel();
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
                // Resume
                if (pauseMenuUI != null)
                    pauseMenuUI.SetActive(false);
                Time.timeScale = 1f;
                _isPaused = false;
                break;
            case 1:
                // Return to MainMenu scene
                Time.timeScale = 1f; // unpause before scene load
                SceneManager.LoadScene("MainMenu");
                break;
            case 2:
                // Settings placeholder
                Debug.Log("PauseMenu: Settings selected (not implemented)");
                break;
        }
    }

    private void HandleSelectionCancel()
    {
        // Treat B as “cancel/pause toggle” → resume
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        _isPaused = false;
    }

    /// <summary>
    /// Call this to open the pause menu.
    /// </summary>
    public void OpenPauseMenu()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        _isPaused = true;
        _currentIndex = 0;
        UpdateActiveSelector();
    }
}
