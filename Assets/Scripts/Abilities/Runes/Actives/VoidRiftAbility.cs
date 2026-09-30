using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// The Hollow — Rune III: Void Rift. Press Y to tear the first portal open at your feet;
/// move, then release Y to open the second where you stand (it opens by itself after
/// maxHoldTime). A quick tap puts the exit ahead of you instead. Once both are down
/// they're linked for `duration` seconds — anyone can step through; you come out faster.
public class VoidRiftAbility : MonoBehaviour, IActiveAbility
{
    [Tooltip("Exit distance along your aim for a quick tap")]
    public float distance = 10f;
    public float duration = 8f;
    public float radius = 0.9f;
    public float ownerSpeed = 1.4f;
    public float ownerSpeedTime = 2f;

    [Header("Placing")]
    [Tooltip("Longest you can hold before the exit opens on its own")]
    public float maxHoldTime = 3f;
    [Tooltip("Releasing closer than this to the first portal counts as a tap (exit goes ahead)")]
    public float minPortalGap = 3f;

    bool placing;

    public void Activate(GameObject caster)
    {
        if (placing) return;
        StartCoroutine(Place(caster));
    }

    IEnumerator Place(GameObject caster)
    {
        placing = true;
        Color theme = AbilityKit.Theme(caster);
        Vector3 a = AbilityKit.Ground(caster.transform.position + Vector3.up);
        var rift = new GameObject("VoidRift").AddComponent<VoidRift>();
        rift.Begin(caster, a, radius, theme, ownerSpeed, ownerSpeedTime);

        // hold Y and walk; release (or run out of time) to open the exit
        var pad = AbilityKit.Pad(caster);
        float held = 0f;
        yield return null; // the press that cast it
        while (held < maxHoldTime && pad != null && pad.buttonNorth.isPressed && caster.activeInHierarchy)
        {
            held += Time.deltaTime;
            yield return null;
        }

        Vector3 here = AbilityKit.Ground(caster.transform.position + Vector3.up);
        Vector3 flat = here - a; flat.y = 0f;
        Vector3 b = flat.magnitude >= minPortalGap ? here : AbilityKit.AimPoint(caster, distance);

        if (rift != null) rift.Link(b, duration);
        placing = false;
    }

    void OnDisable() => placing = false;
}
