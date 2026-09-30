using UnityEngine;

/// <summary>
/// Global pause state. Gameplay input scripts check InputBlocked so buttons pressed
/// to drive the pause menu (A to resume, right stick to navigate) don't also make
/// the players jump, shoot or aim.
/// </summary>
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    // Frame the menu closed on; that frame's A/B press belongs to the menu
    static int resumedFrame = -1;

    public static bool InputBlocked => IsPaused || Time.frameCount == resumedFrame;

    public static void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
    }

    public static void Resume()
    {
        if (IsPaused) resumedFrame = Time.frameCount;
        IsPaused = false;
        Time.timeScale = 1f;
    }

    // Scene reloads keep statics around; never start a match paused
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsPaused = false;
        resumedFrame = -1;
    }
}
