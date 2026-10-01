using UnityEngine;

/// <summary>
/// Root of a baked arena miniature (MiniatureBaker). Real-scale geometry, origin at the
/// centre of the cropped square on the arena floor; the War Table scales it down.
/// </summary>
public class MapMiniature : MonoBehaviour
{
    public string sceneName;
    [Tooltip("Side of the cropped square, in arena units")]
    public float size = 30f;
}
