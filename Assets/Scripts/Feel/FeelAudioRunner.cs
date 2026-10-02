using UnityEngine;
using UnityEngine.SceneManagement;

/// Feeds FeelAudio's announcer queue (FeelAudio itself is static).
[AddComponentMenu("")]
public class FeelAudioRunner : MonoBehaviour
{
    void OnEnable() => SceneManager.activeSceneChanged += SceneChanged;
    void OnDisable() => SceneManager.activeSceneChanged -= SceneChanged;
    void SceneChanged(Scene a, Scene b) => FeelAudio.SceneChanged();
    void Update() => FeelAudio.Tick();
}
