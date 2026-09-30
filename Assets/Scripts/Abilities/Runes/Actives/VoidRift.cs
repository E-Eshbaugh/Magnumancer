using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// The pair of portals made by Void Rift. The first is dormant until Link() opens the
/// second; then anyone stepping into one comes out of the other.
public class VoidRift : MonoBehaviour
{
    GameObject owner;
    Vector3 a, b;
    bool linked;
    float until, radius, ownerSpeed, ownerSpeedTime;
    Color color;
    VoidPortalFx portalA, portalB;
    readonly Dictionary<PlayerMovement3D, float> cooldown = new();
    // players standing in a portal who haven't stepped off it yet
    readonly HashSet<PlayerMovement3D> inside = new();
    float armedAt;

    public void Begin(GameObject caster, Vector3 pa, float r, Color c, float speed, float speedTime)
    {
        owner = caster; a = pa; radius = r; color = c;
        ownerSpeed = speed; ownerSpeedTime = speedTime;
        portalA = VoidPortalFx.Spawn(pa, r);
        AbilityKit.Shockwave(pa, 1.8f, c);
        // safety net if Link never comes (caster removed mid-hold)
        until = Time.time + 10f;
    }

    public void Link(Vector3 pb, float duration)
    {
        if (linked) return;
        linked = true;
        b = pb;
        portalB = VoidPortalFx.Spawn(pb, radius);
        portalA.Open();
        portalB.Open();
        AbilityKit.Shockwave(pb, 1.8f, color);
        until = Time.time + duration;
        armedAt = Time.time + 0.4f;
        // whoever is standing on a portal right now has to step off first
        foreach (var m in FindObjectsByType<PlayerMovement3D>())
            if (Flat(m.transform.position - a) <= radius || Flat(m.transform.position - b) <= radius)
                inside.Add(m);
    }

    void Update()
    {
        if (Time.time >= until) { Close(); return; }
        if (!linked || Time.time < armedAt) return;

        foreach (var m in FindObjectsByType<PlayerMovement3D>())
        {
            Vector3 p = m.transform.position;
            Vector3? exit = null;
            if (Flat(p - a) <= radius) exit = b;
            else if (Flat(p - b) <= radius) exit = a;

            if (exit == null) { inside.Remove(m); continue; }
            if (inside.Contains(m)) continue;
            if (cooldown.TryGetValue(m, out float t) && Time.time < t) continue;

            m.Teleport(exit.Value + Vector3.up * 0.05f, m.transform.rotation);
            cooldown[m] = Time.time + 1.2f; // don't bounce straight back
            inside.Add(m);                  // must step off the exit before using it
            AbilityKit.Shockwave(exit.Value, 1.5f, color, 0.3f);
            Rumble.Play(m.gameObject, 0.3f, 0.6f, 0.15f);
            if (m.gameObject == owner) StartCoroutine(Boost(m));
        }
    }

    void Close()
    {
        if (portalA != null) portalA.Close();
        if (portalB != null) portalB.Close();
        enabled = false;
        Destroy(gameObject, ownerSpeedTime + 0.1f); // let an in-progress speed boost finish
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    IEnumerator Boost(PlayerMovement3D m)
    {
        m.SetSpeedModifier("voidrift", ownerSpeed);
        yield return new WaitForSeconds(ownerSpeedTime);
        if (m != null) m.ClearSpeedModifier("voidrift");
    }

    void OnDestroy()
    {
        if (owner != null && owner.TryGetComponent<PlayerMovement3D>(out var m)) m.ClearSpeedModifier("voidrift");
    }
}
