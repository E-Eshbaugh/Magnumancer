using UnityEngine;
using System.Collections;

public class StunEffect : MonoBehaviour
{
    private PlayerMovement3D movement;
    private GunOrbitController orbit;
    private Coroutine stunRoutine;

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        orbit = GetComponentInChildren<GunOrbitController>();
    }

    public void ApplyStun(float moveMultiplier, float orbitMultiplier, float duration)
    {
        if (stunRoutine != null)
            StopCoroutine(stunRoutine);

        stunRoutine = StartCoroutine(StunCoroutine(moveMultiplier, orbitMultiplier, duration));
    }

    private IEnumerator StunCoroutine(float moveMult, float orbitMult, float time)
    {
        if (movement != null)
        {
            movement.SetMoveSpeedMultiplier(moveMult);
            movement.SetJumpBlocked("stun", true); // stunned/rooted/frozen: no jumping out of it
        }

        if (orbit != null)
            orbit.SetOrbitSpeedMultiplier(orbitMult); // You'll need to implement this

        yield return new WaitForSeconds(time);

        if (movement != null)
        {
            movement.SetMoveSpeedMultiplier(1f);
            movement.SetJumpBlocked("stun", false);
        }

        if (orbit != null)
            orbit.SetOrbitSpeedMultiplier(1f);

        stunRoutine = null;
    }

    // Never leave a player grounded forever if the stun is cut short
    void OnDisable()
    {
        if (stunRoutine == null) return;
        stunRoutine = null;
        if (movement != null)
        {
            movement.SetMoveSpeedMultiplier(1f);
            movement.SetJumpBlocked("stun", false);
        }
        if (orbit != null) orbit.SetOrbitSpeedMultiplier(1f);
    }
}
