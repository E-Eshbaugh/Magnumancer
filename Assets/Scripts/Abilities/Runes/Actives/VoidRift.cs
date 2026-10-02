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
    readonly Dictionary<GameObject, float> cooldown = new();
    // players standing in a portal who haven't stepped off it yet
    readonly HashSet<GameObject> inside = new();
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
        foreach (var target in Travellers())
            if (Flat(target.transform.position - a) <= radius || Flat(target.transform.position - b) <= radius)
                inside.Add(target);
    }

    void Update()
    {
        if (Time.time >= until) { Close(); return; }
        if (!linked || Time.time < armedAt) return;

        foreach (var target in Travellers())
        {
            Vector3 p = target.transform.position;
            Vector3? exit = null;
            if (Flat(p - a) <= radius) exit = b;
            else if (Flat(p - b) <= radius) exit = a;

            if (exit == null) { inside.Remove(target); continue; }
            if (inside.Contains(target)) continue;
            if (cooldown.TryGetValue(target, out float t) && Time.time < t) continue;

            var m = target.GetComponent<PlayerMovement3D>();
            if (m != null) m.Teleport(exit.Value + Vector3.up * 0.05f, m.transform.rotation);
            else
            {
                var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!UnityEngine.AI.NavMesh.SamplePosition(exit.Value, out var landing, 1.5f, filter)
                    || !agent.Warp(landing.position)) continue;
            }
            cooldown[target] = Time.time + 1.2f; // don't bounce straight back
            inside.Add(target);                  // must step off the exit before using it
            AbilityKit.Shockwave(exit.Value, 1.5f, color, 0.3f);
            Rumble.Play(target, 0.3f, 0.6f, 0.15f);
            if (m != null && target == owner) StartCoroutine(Boost(m));
        }
    }

    static IEnumerable<GameObject> Travellers()
    {
        foreach (var player in FindObjectsByType<PlayerMovement3D>())
            if (DamageEvents.IsAlive(player.gameObject)) yield return player.gameObject;
        foreach (var monster in FindObjectsByType<GoblinHealth>())
            if (DamageEvents.IsAlive(monster.gameObject)) yield return monster.gameObject;
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
