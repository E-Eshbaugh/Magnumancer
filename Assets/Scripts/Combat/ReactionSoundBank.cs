using UnityEngine;

/// Which clips each reaction plays (see ReactionAudio). The one in use is
/// Resources/ReactionSounds.asset.
[CreateAssetMenu(menuName = "Magnumancer/Reaction Sound Bank", fileName = "ReactionSounds")]
public class ReactionSoundBank : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Reaction reaction;
        public AudioClip[] clips;       // one picked at random
        [Range(0f, 1f)] public float volume = 0.8f;
        public float pitch = 1f;
        [Tooltip("Seconds of the clip to play (0 = all of it)")]
        public float maxLength = 1.5f;
    }

    [Range(0f, 1f)] public float masterVolume = 0.8f;
    public Entry[] reactions;

    [Header("Combo announcer")]
    public AudioClip comboFrenzy;       // 4 reactions in a row
    public AudioClip comboOverload;     // 5+
    [Range(0f, 1f)] public float announcerVolume = 0.9f;

    public Entry Find(Reaction r)
    {
        if (reactions == null) return null;
        foreach (var e in reactions) if (e != null && e.reaction == r) return e;
        return null;
    }
}
