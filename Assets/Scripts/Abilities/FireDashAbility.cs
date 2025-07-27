using UnityEngine;
using Magnumancer.Abilities;
using UnityEngine.InputSystem; // or your actual namespace

public class FireDashAbility : MonoBehaviour, IActiveAbility
{
    [Tooltip("Meters to travel if unobstructed.")]
    [SerializeField] float fireDashDistance = 8f;
    [Tooltip("Seconds dash takes to complete.")]
    [SerializeField] float fireDashTime = 0.15f;
    [Tooltip("Rotate stick input for isometric worlds.")]
    [SerializeField] float fireDashIsoYaw = 45f;

    CharacterController _cc;
    bool _fireDashing;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    public void Activate(GameObject caster)
    {
        Debug.Log("FireDash activated!");
        if (_fireDashing) return;

        Vector2 stick = Vector2.zero;
        var playerInput = caster.GetComponent<PlayerInput>();
        if (playerInput != null && Gamepad.current != null)
        {
            stick = Gamepad.current.leftStick.ReadValue();
        }

        Vector3 dir3 = new Vector3(stick.x, 0f, stick.y);

        if (dir3.sqrMagnitude < 0.0001f)
            dir3 = caster.transform.forward;
        else
        {
            if (Mathf.Abs(fireDashIsoYaw) > 0.01f)
                dir3 = Quaternion.Euler(0f, fireDashIsoYaw, 0f) * dir3;

            dir3.Normalize();
        }

        caster.transform.forward = dir3;
        caster.GetComponent<MonoBehaviour>().StartCoroutine(FireDashRoutine(caster, dir3));
    }

    System.Collections.IEnumerator FireDashRoutine(GameObject caster, Vector3 dir)
    {
        _fireDashing = true;

        float elapsed = 0f;
        Vector3 startPos = caster.transform.position;
        float totalDist = fireDashDistance;
        Vector3 stepVel = dir * (totalDist / fireDashTime);

        CharacterController cc = caster.GetComponent<CharacterController>();

        while (elapsed < fireDashTime)
        {
            float dt = Time.deltaTime;
            Vector3 step = stepVel * dt;

            if (cc != null)
                cc.Move(step);
            else
                caster.transform.position += step;

            elapsed += dt;
            yield return null;
        }

        Vector3 desiredEnd = startPos + dir * totalDist;
        Vector3 remain = desiredEnd - caster.transform.position;

        if (cc != null)
            cc.Move(remain);
        else
            caster.transform.position += remain;

        _fireDashing = false;
    }
}
