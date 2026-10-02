using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared helpers for the rune abilities: aiming, finding enemies, knockback and simple
/// glowing visuals in the caster's theme color.
/// </summary>
public static class AbilityKit
{
    public static PlayerMovement3D Movement(GameObject caster)
        => caster != null ? caster.GetComponentInParent<PlayerMovement3D>() : null;

    public static Gamepad Pad(GameObject caster) => Movement(caster)?.gamepad;

    public static WizardData Wizard(GameObject caster) => Movement(caster)?.wizard;

    public static Color Theme(GameObject caster)
        => GlowLine.Brighten(WizardShade.Of(caster));   // the caster's rune shade

    /// Flat direction the caster's gun points
    public static Vector3 AimDir(GameObject caster)
    {
        var orbit = caster != null ? caster.GetComponentInChildren<GunOrbitController>() : null;
        Vector3 d = orbit != null ? orbit.aimDirection : (caster != null ? caster.transform.forward : Vector3.forward);
        d.y = 0f;
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    /// Where the caster is steering (stick), or facing if the stick is idle
    public static Vector3 MoveDir(GameObject caster)
    {
        var m = Movement(caster);
        if (m != null && m.gamepad != null)
        {
            Vector2 s = m.gamepad.leftStick.ReadValue();
            if (s.sqrMagnitude > 0.04f)
                return (Quaternion.Euler(0, 45f, 0) * new Vector3(s.x, 0, s.y)).normalized;
        }
        Vector3 f = caster.transform.forward; f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    /// A spot `distance` along the aim, pulled short if a wall is in the way, snapped to the ground
    public static Vector3 AimPoint(GameObject caster, float distance)
    {
        Vector3 origin = caster.transform.position + Vector3.up;
        Vector3 dir = AimDir(caster);
        if (Physics.Raycast(origin, dir, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(caster.transform))
            distance = Mathf.Max(0.5f, hit.distance - 0.5f);
        return Ground(origin + dir * distance);
    }

    static readonly RaycastHit[] groundHits = new RaycastHit[16];

    /// The floor below p. Skips players and monsters (casting at your own feet used to
    /// land on top of your own head) and loose debris, so effects sit on the real ground.
    public static Vector3 Ground(Vector3 p)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 3f, Vector3.down, groundHits, 20f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 point = new Vector3(p.x, p.y - 1f, p.z);
        for (int i = 0; i < n; i++)
        {
            var h = groundHits[i];
            if (h.distance >= best) continue;
            var root = DamageEvents.RootOf(h.collider);
            if (DamageEvents.IsCombatant(root)) continue;                                   // a player or monster
            if (h.collider.GetComponentInParent<PlayerMovement3D>() != null) continue;       // their gun, clone, etc.
            if (h.collider.GetComponentInParent<Bullet>() != null) continue;
            best = h.distance;
            point = h.point;
        }
        return point;
    }

    static Collider[] buffer = new Collider[64];

    /// Living enemies (other players, monsters) with a collider inside the sphere
    public static List<GameObject> Enemies(Vector3 center, float radius, GameObject caster, bool includeTeammates = false)
    {
        var found = new List<GameObject>();
        int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // A horde (or multi-collider enemies) must not silently fill the query buffer.
        while (n == buffer.Length)
        {
            System.Array.Resize(ref buffer, buffer.Length * 2);
            n = Physics.OverlapSphereNonAlloc(center, radius, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        for (int i = 0; i < n; i++)
        {
            var root = DamageEvents.RootOf(buffer[i]);
            if (root == null || root == caster || found.Contains(root)) continue;
            if (!DamageEvents.IsAlive(root)) continue;
            if (!Teams.CanHarm(root, caster)) continue;
            if (!includeTeammates && Teams.SameTeam(root, caster)) continue;
            found.Add(root);
        }
        return found;
    }

    /// Nearest enemy within range, preferring ones inside the aim cone
    public static GameObject NearestEnemy(GameObject caster, float range, float coneDegrees = 360f)
    {
        Vector3 from = caster.transform.position;
        Vector3 aim = AimDir(caster);
        GameObject best = null;
        float bestScore = float.MaxValue;
        foreach (var e in Enemies(from, range, caster))
        {
            Vector3 to = e.transform.position - from; to.y = 0f;
            float angle = Vector3.Angle(aim, to);
            if (angle > coneDegrees * 0.5f) continue;
            float score = to.magnitude * (1f + angle / 90f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    public static void Knockback(GameObject target, Vector3 force, GameObject source = null)
    {
        if (!Teams.CanHarm(target, source)) return;
        var m = target != null ? target.GetComponent<PlayerMovement3D>() : null;
        if (m != null) m.ApplyKnockback(force);
        else if (target != null && target.TryGetComponent<GoblinHealth>(out var monster) && !monster.IsDead)
            StatusEffects.Of(target).Knockback(force);
    }

    /// Area spells ignore their source and combatant bodies, but solid cover still blocks them.
    public static bool ClearPath(Vector3 from, GameObject target, GameObject source)
    {
        foreach (var hit in Physics.RaycastAll(from, Chest(target) - from,
                     Vector3.Distance(from, Chest(target)), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (source != null && hit.transform.IsChildOf(source.transform)) continue;
            if (hit.transform.IsChildOf(target.transform) || DamageEvents.IsCombatant(DamageEvents.RootOf(hit.collider))) continue;
            return false;
        }
        return true;
    }

    /// Runtime cover must also cut a hole in the baked goblin navigation mesh.
    public static void BlockNavigation(MeshCollider collider)
    {
        if (collider == null || collider.sharedMesh == null) return;
        var obstacle = collider.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle == null) obstacle = collider.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.center = collider.sharedMesh.bounds.center;
        obstacle.size = collider.sharedMesh.bounds.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
    }

    public static Vector3 Chest(GameObject go) => go.transform.position + Vector3.up * 1.1f;

    // ---------- visuals ----------

    static Material sharedGlow;
    public static Material Glow() => sharedGlow != null ? sharedGlow : (sharedGlow = GlowLine.CreateMaterial(2.5f));

    public static void Circle(LineRenderer lr, Vector3 center, float radius)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }

    /// Expanding, fading ring (impact marker)
    public static void Shockwave(Vector3 center, float radius, Color color, float time = 0.4f)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = center;
        go.AddComponent<ShockwaveFx>().Init(center + Vector3.up * 0.08f, radius, color, time);
    }

    /// Quick lightning line between two points
    public static void Zap(Vector3 a, Vector3 b, Color color, float time = 0.2f, float width = 0.2f)
    {
        var go = new GameObject("Zap");
        var lr = GlowLine.Make(go.transform, "Bolt", 10, width, Glow());
        GlowLine.Bolt(lr, a, b, 0.35f);
        GlowLine.SetColor(lr, Color.Lerp(color, Color.white, 0.3f), 1f);
        Object.Destroy(go, time);
    }

    /// Glowing ball (bombs, mortar shells)
    public static GameObject GlowOrb(Color color, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "GlowOrb";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Glow();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        var c = color * 2f; c.a = 1f;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
        return go;
    }

    /// Moves `obj` along an arc from `from` to `to` over `time` seconds
    public static System.Collections.IEnumerator Lob(Transform obj, Vector3 from, Vector3 to, float time, float height)
    {
        float t = 0f;
        while (t < time && obj != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            obj.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * height * k * (1f - k));
            yield return null;
        }
    }
}
