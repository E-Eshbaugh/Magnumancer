using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GameInitializer : MonoBehaviour
{
    [Tooltip("Target resolution aspect ratio, e.g. 16:9 = 1.777")]
    public float targetAspectRatio = 16f / 9f;

    void Awake()
    {
        Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
        Screen.SetResolution(1920, 1080, true);
    }

    void Start()
    {
        Camera cam = Camera.main;

        float screenAspect = (float)Screen.width / Screen.height;
        float scaleHeight = screenAspect / targetAspectRatio;

        if (scaleHeight < 1f)
        {
            Rect rect = cam.rect;
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
            cam.rect = rect;
        }
        else
        {
            float scaleWidth = 1f / scaleHeight;
            Rect rect = cam.rect;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
            cam.rect = rect;
        }
    }
}
