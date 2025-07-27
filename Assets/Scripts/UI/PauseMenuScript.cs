using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenScript: MonoBehaviour
{
    [Tooltip("Root GameObject of your pause menu UI")]
    public GameObject pauseMenuUI;

    private bool _isPaused = false;
    private Gamepad _gamepad;

    void Start()
    {
        // Ensure menu is hidden initially
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
    }

    void Update()
    {
        // Grab the assigned gamepad (or the first connected)
        if (_gamepad == null)
            _gamepad = Gamepad.current;

        // Check for menu/start button or Escape key
        bool menuPressed = (_gamepad != null && 
                            (_gamepad.startButton.wasPressedThisFrame 
                             || _gamepad.startButton?.wasPressedThisFrame == true))
                           || Keyboard.current.escapeKey.wasPressedThisFrame;

        if (menuPressed)
            TogglePause();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;

        // Show/hide UI
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(_isPaused);

        // Freeze/unfreeze time
        Time.timeScale = _isPaused ? 0f : 1f;

        // (Optional) lock/unlock the cursor
        Cursor.visible = _isPaused;
        Cursor.lockState = _isPaused 
            ? CursorLockMode.None 
            : CursorLockMode.Locked;
    }
}
