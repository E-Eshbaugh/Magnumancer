using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenScript: MonoBehaviour
{
    [Tooltip("Root GameObject of your pause menu UI")]
    public GameObject pauseMenuUI;

    private PauseUIControl menuControl;

    void Start()
    {
        if (pauseMenuUI != null)
        {
            menuControl = pauseMenuUI.GetComponent<PauseUIControl>();
            pauseMenuUI.SetActive(false);
        }
        GamePause.Resume();
    }

    void Update()
    {
        // Any connected controller's Start (or Escape) toggles the menu
        bool menuPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        Gamepad presser = null;
        foreach (var pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
            {
                menuPressed = true;
                presser = pad;
                break;
            }
        }

        if (menuPressed)
            TogglePause(presser);
    }

    private void TogglePause(Gamepad presser)
    {
        // PauseUIControl can close the menu itself (Resume / B), so read the
        // menu's real state instead of keeping our own flag.
        bool open = pauseMenuUI != null ? !pauseMenuUI.activeSelf : !GamePause.IsPaused;

        if (open)
        {
            if (menuControl != null) menuControl.OpenPauseMenu(presser);
            else
            {
                if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
                GamePause.Pause();
            }
        }
        else
        {
            if (menuControl != null) menuControl.ClosePauseMenu();
            else
            {
                if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
                GamePause.Resume();
            }
        }

        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
