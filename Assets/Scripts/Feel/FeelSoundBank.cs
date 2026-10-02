using UnityEngine;

/// <summary>
/// Clips for hit feel, the announcer and music. Lives at Resources/FeelSounds; swap clips
/// there. Announcer lines load by name from Resources/Announcer (first_blood, round_3,
/// wins_the_hollow...), so replacing a placeholder voice is just dropping in a file of
/// the same name. Leave the music clips empty to use the built-in synth score.
/// </summary>
[CreateAssetMenu(menuName = "Magnumancer/Feel Sound Bank")]
public class FeelSoundBank : ScriptableObject
{
    [Header("Hits")]
    [Tooltip("A normal hit landing on a wizard (one picked at random)")]
    public AudioClip[] hurt;
    [Tooltip("A big hit (a large share of their health in one go)")]
    public AudioClip[] heavyHurt;
    [Range(0f, 1f)] public float hurtVolume = 0.55f;

    [Header("Announcer")]
    [Range(0f, 1f)] public float announcerVolume = 1f;
    [Tooltip("How far the music dips while the announcer talks")]
    [Range(0f, 1f)] public float musicDuck = 0.45f;

    [Header("Music (optional: empty = the built-in synth score)")]
    public AudioClip hallMusic;
    [Tooltip("Battle tracks; one is picked per match")]
    public AudioClip[] battleMusic;
    public AudioClip victoryStinger;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
}
