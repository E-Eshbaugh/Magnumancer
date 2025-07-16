using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class CircleAbilityUI : MonoBehaviour
{
    [Header("UI")]
    public Image glowImage;         // your soft-edge circle
    public Image crestImage;        // for sampling color

    [Header("Charge Settings")]
    public float rechargeTime = 5f;
    public float scale = 1.2f;      // fixed scale
    public float maxAlpha = 0.8f;   // alpha at full charge
    [Tooltip("How much brighter the pulse peak is (e.g. 1.2 = +20%)")]
    public float pulseIntensity = 1.2f;
    [Tooltip("Speed of the pulse when full")]
    public float pulseSpeed = 3f;

    [Header("Controller")]
    [Tooltip("The specific gamepad to listen to for ability input")]
    public Gamepad gamepad;         // assign in inspector or from MultiplayerManager
    private float timer;
    private Coroutine pulseRoutine;
    private Color sampledCrestColor;

    void Start()
    {
        // sample the sprite’s center pixel
        Sprite sprite = crestImage.sprite;
        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;
        int x = Mathf.FloorToInt(r.x + r.width * 0.5f);
        int y = Mathf.FloorToInt(r.y + r.height * 0.5f);
        sampledCrestColor = tex.GetPixel(x, y);
        sampledCrestColor.a = 1f;

        // set fixed scale
        glowImage.rectTransform.localScale = Vector3.one * scale;
    }

    void Update()
    {
        // advance recharge
        timer = Mathf.Clamp(timer + Time.deltaTime, 0f, rechargeTime);
        float frac = timer / rechargeTime;

        // compute base alpha
        float baseA = frac * maxAlpha;

        // apply color + alpha (pulse coroutine will override alpha when running)
        glowImage.color = new Color(
            sampledCrestColor.r,
            sampledCrestColor.g,
            sampledCrestColor.b,
            baseA
        );

        // start or stop pulse routine
        if (frac >= 1f && pulseRoutine == null)
            pulseRoutine = StartCoroutine(PulseAlpha(baseA));
        else if (frac < 1f && pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
            // ensure alpha resets to zero
            var c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }

        // only use the assigned gamepad’s West (B) button
        if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame && frac >= 1f)
        {
            UseAbility();
        }
    }

    private IEnumerator PulseAlpha(float baseAlpha)
    {
        while (true)
        {
            // sine between 1−(pulseIntensity−1) and pulseIntensity
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;  // 0→1
            float intensity = Mathf.Lerp(1f - (pulseIntensity - 1f),
                                         pulseIntensity,
                                         t);
            float a = baseAlpha * intensity;
            var c = glowImage.color;
            c.a = a;
            glowImage.color = c;
            yield return null;
        }
    }

    private void UseAbility()
    {
        // reset timer & alpha
        timer = 0f;
        var c = glowImage.color;
        c.a = 0f;
        glowImage.color = c;

        // stop pulse
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        Debug.Log("Ability used!");
        // TODO: your ability activation logic
    }
}
