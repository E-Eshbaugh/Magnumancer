using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Great Hall's camera: the same orthographic 30°/45° isometric view as the arenas,
/// rendered into a low-res texture for the game's pixel look (the arenas do the same
/// with a 320x180 "Pixelation" image). Moves between shots (the whole hall, the War
/// Table, a dive into one miniature) with eased transitions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HallCamera : MonoBehaviour
{
    [Header("Pixel look")]
    [Tooltip("Height of the low-res render; width follows 16:9. The arenas use 180; the hall uses more so guns and miniatures stay readable.")]
    public int pixelHeight = 360;

    [Header("Iso view")]
    public Vector3 viewEuler = new Vector3(30f, 45f, 0f);
    public float distance = 60f;

    [Header("Shots")]
    public Vector3 hallFocus = new Vector3(0f, 1.2f, 0f);
    public float hallSize = 11f;
    public Vector3 tableFocus = new Vector3(0f, 2.3f, 0f);
    public float tableSize = 4.6f;

    [Header("Life")]
    [Tooltip("Slow drift so the hall never looks frozen")]
    public float swayAmount = 0.12f;
    public float swaySpeed = 0.15f;

    public Camera Cam { get; private set; }
    public RenderTexture Target { get; private set; }

    Vector3 focus;
    float size;
    Coroutine move;
    RawImage screen;

    void Awake()
    {
        Cam = GetComponent<Camera>();
        Cam.orthographic = true;
        transform.rotation = Quaternion.Euler(viewEuler);
        focus = hallFocus;
        size = hallSize;
        BuildTarget();
        Apply();
    }

    void BuildTarget()
    {
        int h = Mathf.Max(90, pixelHeight);
        int w = Mathf.RoundToInt(h * 16f / 9f);
        Target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = "HallPixels",
            filterMode = FilterMode.Point,
            antiAliasing = 1,
        };
        Target.Create();
        Cam.targetTexture = Target;

        // a canvas behind the menu UI that shows the pixel render full-screen
        var canvasGo = new GameObject("HallScreen", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        var img = new GameObject("Pixels", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        screen = img.GetComponent<RawImage>();
        screen.texture = Target;
        screen.raycastTarget = false;
        var fit = img.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;
    }

    void OnDestroy()
    {
        if (Target != null) { Cam.targetTexture = null; Target.Release(); Destroy(Target); }
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        float t = Time.unscaledTime * swaySpeed;
        var sway = new Vector3(Mathf.Sin(t * 1.3f), 0f, Mathf.Sin(t * 0.9f + 1.7f)) * swayAmount * (size / hallSize);
        transform.rotation = Quaternion.Euler(viewEuler);
        transform.position = focus + sway - transform.forward * distance;
        Cam.orthographicSize = size;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = distance * 3f;
    }

    public bool Moving => move != null;

    public void ToHall(float time = 1.1f) => MoveTo(hallFocus, hallSize, time);
    public void ToTable(float time = 1.3f) => MoveTo(tableFocus, tableSize, time);

    public void MoveTo(Vector3 newFocus, float newSize, float time)
    {
        if (move != null) StopCoroutine(move);
        move = StartCoroutine(Move(newFocus, newSize, time));
    }

    /// Snaps straight to a shot (scene start, tests)
    public void Cut(Vector3 newFocus, float newSize)
    {
        if (move != null) { StopCoroutine(move); move = null; }
        focus = newFocus; size = newSize;
        Apply();
    }

    public void CutToHall() => Cut(hallFocus, hallSize);

    IEnumerator Move(Vector3 toFocus, float toSize, float time)
    {
        Vector3 f0 = focus; float s0 = size;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / time);
            k = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;   // ease in-out cubic
            focus = Vector3.Lerp(f0, toFocus, k);
            // zoom in log space so it feels even
            size = Mathf.Exp(Mathf.Lerp(Mathf.Log(s0), Mathf.Log(toSize), k));
            yield return null;
        }
        focus = toFocus; size = toSize;
        move = null;
    }

    /// Where a world point lands on screen, in 0..1 viewport units
    public Vector3 Viewport(Vector3 world) => Cam.WorldToViewportPoint(world);

    /// Screen-right and screen-up directions along the floor
    public static Vector3 GroundRight => new Vector3(1f, 0f, -1f).normalized;
    public static Vector3 GroundUp => new Vector3(1f, 0f, 1f).normalized;
    /// Facing that looks straight at the camera
    public static Quaternion FacingCamera => Quaternion.LookRotation(-GroundUp, Vector3.up);
}
