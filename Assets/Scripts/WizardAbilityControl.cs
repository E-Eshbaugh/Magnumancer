using UnityEngine;
using UnityEngine.InputSystem;

public class WizardAbilityController : MonoBehaviour
{
    public Gamepad gamepad;
    public WizardData wizard;
    public bool isUsingAbility = false;

    [Header("Fire Dash Settings")]
    [Tooltip("Meters to travel if unobstructed.")]
    [SerializeField] float fireDashDistance = 8f;
    [Tooltip("Seconds dash takes to complete.")]
    [SerializeField] float fireDashTime = 0.15f;
    [Tooltip("Rotate stick input for isometric worlds (e.g., 45°). Set to 0 for none.")]
    [SerializeField] float fireDashIsoYaw = 45f;

    CharacterController _cc;      // optional
    bool _fireDashing;            // internal

    void Start()
    {
        _cc = GetComponent<CharacterController>(); // ok if null
    }

    void Update()
    {
        if (gamepad == null) return;

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            TriggerAbility();
        }
    }

    void TriggerAbility()
    {
        Vector2 stick = gamepad.leftStick.ReadValue();

        if (!isUsingAbility)
        {
            isUsingAbility = true;
            switch (wizard.wizardName)
            {
                case "Emberguard":
                    FireDash(stick);
                    break;
                default:
                    Debug.LogWarning("Unknown ability type: " + wizard.wizardName);
                    isUsingAbility = false; // release if nothing fired
                    break;
            }
        }
    }

    // ------------------------------------------------------------------
    // FIRE DASH (minimal prototype)
    // ------------------------------------------------------------------
    void FireDash(Vector2 direction)
    {
        if (_fireDashing) return; // safety

        // Convert 2D stick to world space
        Vector3 dir3 = new Vector3(direction.x, 0f, direction.y);

        // If stick is neutral, dash forward
        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = transform.forward;
        else
        {
            // Apply optional isometric rotation (about Y axis)
            if (Mathf.Abs(fireDashIsoYaw) > 0.01f)
                dir3 = Quaternion.Euler(0f, fireDashIsoYaw, 0f) * dir3;

            dir3.Normalize();
        }

        // Face dash direction immediately (helps aim readability)
        if (dir3.sqrMagnitude > 0.0001f)
            transform.forward = dir3;

        StartCoroutine(FireDashRoutine(dir3));
    }

    System.Collections.IEnumerator FireDashRoutine(Vector3 worldDir)
    {
        _fireDashing = true;

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        float totalDist = fireDashDistance;
        Vector3 stepVel = worldDir * (totalDist / fireDashTime); // m/s

        while (elapsed < fireDashTime)
        {
            float dt = Time.deltaTime;
            Vector3 step = stepVel * dt;

            // CharacterController handles collision gracefully
            if (_cc != null)
                _cc.Move(step);
            else
                transform.position += step;

            elapsed += dt;
            yield return null;
        }

        // Clean finish: snap any small remainder (in case of frame drift)
        Vector3 desiredEnd = startPos + worldDir * totalDist;
        Vector3 remain = desiredEnd - transform.position;

        if (_cc != null)
            _cc.Move(remain);
        else
            transform.position += remain;

        _fireDashing = false;
        isUsingAbility = false; // release so ability can be triggered again
    }
}
