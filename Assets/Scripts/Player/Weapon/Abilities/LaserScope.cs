using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class LaserScope : MonoBehaviour
{
    [Tooltip("Where the laser originates")]
    public Transform firePoint;

    [Tooltip("Layers the laser can hit (default Everything)")]
    public LayerMask hitLayers = ~0;

    [Header("Controller")]
    public Gamepad gamepad;   // assigned via MultiplayerManager

    [Header("Appearance")]
    public Material mat;      // assign your unlit red here!

    private LineRenderer lr;
    private GunOrbitController orbit;
    private AmmoControl ammoControl;

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
    }

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        orbit = GetComponentInChildren<GunOrbitController>();
        ammoControl = GetComponentInParent<AmmoControl>();
    }

    void OnEnable()
    {
        // Always re-apply material & gradient whenever we turn on
        lr.enabled = false;
        lr.material = mat;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = 0.1f;

        var gradient = new Gradient
        {
            mode = GradientMode.Blend,
            colorKeys = new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.red, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        };
        lr.colorGradient = gradient;
    }

    void Update()
    {
        if (gamepad == null || orbit == null || ammoControl == null || firePoint == null)
        {
            lr.enabled = false;
            return;
        }

        float laserRange = ammoControl.currentGun?.laserRange ?? 0f;
        if (laserRange <= 0f)
        {
            lr.enabled = false;
            return;
        }

        if (gamepad.leftTrigger.ReadValue() > 0.1f)
        {
            Vector3 origin = firePoint.position;
            Vector3 dir = orbit.aimDirection;

            if (Physics.Raycast(origin, dir, out var hit, laserRange, hitLayers))
                lr.SetPosition(1, hit.point);
            else
                lr.SetPosition(1, origin + dir * laserRange);

            lr.SetPosition(0, origin);
            lr.enabled = true;
        }
        else
        {
            lr.enabled = false;
        }
    }
}
