using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class LaserScope : MonoBehaviour
{
    [Tooltip("Where the laser originates")]
    public Transform firePoint;

    [Tooltip("Layers the laser can hit (default Everything)")]
    public LayerMask hitLayers = ~0;

    private LineRenderer lr;
    private Gamepad pad => Gamepad.current;
    public Material mat;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount  = 2;
        lr.useWorldSpace  = true;
        lr.startWidth     = lr.endWidth = 0.1f;

        // Unlit red material
        lr.material = mat;

        // **Add a color gradient that tapers alpha from 1 → 0**
        var gradient = new Gradient();
        gradient.mode = GradientMode.Blend;
        // we want the same red at both ends
        var red = Color.red;
        gradient.colorKeys = new[] {
            new GradientColorKey(red, 0f),
            new GradientColorKey(red, 1f)
        };
        // but alpha goes 1→0
        gradient.alphaKeys = new[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0f, 1f)
        };
        lr.colorGradient = gradient;

        lr.enabled = false;
    }


    void Update()
    {
        // Find laserRange from the equipped weapon data
        var holder = GetComponentInParent<AmmoControl>();
        float laserRange = (holder != null) ? holder.currentGun.laserRange : 0f;

        // If no scope, bail
        if (laserRange <= 0f)
        {
            lr.enabled = false;
            return;
        }

        // Only draw while LT held
        if (pad != null && pad.leftTrigger.ReadValue() > 0.1f)
        {
            Vector3 origin = firePoint.position;
            // Inverted if your firePoint points backwards
            Vector3 dir = -firePoint.forward;
            dir.y = 0f;
            dir.Normalize();

            // Raycast to see if we hit something
            RaycastHit hit;
            Vector3 end;
            if (Physics.Raycast(origin, dir, out hit, laserRange, hitLayers))
            {
                end = hit.point;
            }
            else
            {
                end = origin + dir * laserRange;
            }

            // Draw the line
            lr.SetPosition(0, origin);
            lr.SetPosition(1, end);
            lr.enabled = true;
        }
        else
        {
            lr.enabled = false;
        }
    }
}
