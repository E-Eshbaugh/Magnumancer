using System.Collections.Generic;
using UnityEngine;

/// The planted state + stone shield for Bastion Stance (lives on the player)
public class BastionStance : MonoBehaviour
{
    public float shieldRadius = 1.4f;
    public float arcDegrees = 115f;
    public int slabs = 6;
    public float slabHeight = 2.1f;
    public float riseTime = 0.22f;
    public float turnSpeed = 10f;
    [Tooltip("Moving this far from where you planted (a dash) ends the stance")]
    public float breakDistance = 0.8f;

    PlayerMovement3D movement;
    float until, started, savedKnockback = 1f;
    Vector3 plantedAt;
    bool active;
    Color color;
    GameObject shield;
    readonly List<Transform> slabList = new();
    readonly List<float> slabHeights = new();
    readonly List<LineRenderer> seams = new();
    LineRenderer ring;

    public bool Active => active;

    void Awake() => movement = GetComponent<PlayerMovement3D>();

    public void Begin(float duration, Color theme)
    {
        if (active) End(false);
        color = theme;
        active = true;
        started = Time.time;
        until = Time.time + duration;
        plantedAt = transform.position;

        if (movement != null)
        {
            savedKnockback = movement.knockbackMultiplier;
            movement.knockbackMultiplier = 0f;           // immovable
            movement.SetSpeedModifier("bastion", 0f);    // planted
        }

        // brace: the ground slams and cracks
        Vector3 ground = AbilityKit.Ground(transform.position + Vector3.up);
        AbilityKit.Shockwave(ground, 3.5f, color, 0.4f);
        RockDebris.Burst(ground, 12, 6f, 0.3f);
        RockDebris.Dust(ground, 3f, 20);
        PowerFx.Flash(ground + Vector3.up, color, 6f, 6f, 0.4f);
        CameraShake.Shake(0.3f, 0.25f);
        Rumble.Play(gameObject, 1f, 0.6f, 0.35f);

        BuildShield(ground);
    }

    void BuildShield(Vector3 ground)
    {
        shield = new GameObject("BastionShield");
        shield.transform.position = ground;
        shield.transform.rotation = Quaternion.LookRotation(AbilityKit.AimDir(gameObject));
        var stone = RockDebris.StoneMaterial(Color.gray);
        var glow = AbilityKit.Glow();

        var owned = shield.AddComponent<OwnedMeshes>();

        slabList.Clear(); slabHeights.Clear(); seams.Clear();
        for (int i = 0; i < slabs; i++)
        {
            float f = slabs == 1 ? 0.5f : i / (float)(slabs - 1);
            float ang = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, f);
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;

            // a broken sheet of rock: overlapping slabs, each with its own lean and a ragged peak
            float h = slabHeight * Random.Range(0.95f, 1.25f);
            float hw = Random.Range(0.4f, 0.5f), hd = Random.Range(0.18f, 0.25f);
            var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-7f, 4f), Random.Range(-10f, 10f), Random.Range(-8f, 8f));
            var slab = RockShapes.Spawn(shield.transform, owned, "BastionSlab", SlabRings(h, hw, hd),
                                        new Vector3(Random.Range(-0.35f, 0.35f) * hw, h, Random.Range(-0.05f, 0.05f)),
                                        i % 3 == 1 ? RockShapes.DarkStone() : stone,
                                        dir * shieldRadius + Vector3.up * (-h - 0.05f), rot);   // starts buried
            var mc = slab.AddComponent<MeshCollider>();
            mc.sharedMesh = slab.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            AbilityKit.BlockNavigation(mc);
            slab.AddComponent<BastionShieldPiece>().Init(this);
            slabList.Add(slab.transform);
            slabHeights.Add(h);

            // a glowing rune crack zig-zagging up each slab's face
            var seam = GlowLine.Make(slab.transform, "Seam", 4, 0.06f, glow);
            seam.useWorldSpace = false;
            float z = hd * 1.2f, jag = Random.value < 0.5f ? 0.09f : -0.09f;
            seam.SetPosition(0, new Vector3(0f, h * 0.25f, z));
            seam.SetPosition(1, new Vector3(jag, h * 0.42f, z));
            seam.SetPosition(2, new Vector3(-jag * 0.6f, h * 0.58f, z));
            seam.SetPosition(3, new Vector3(jag * 0.4f, h * 0.74f, z * 0.9f));
            seams.Add(seam);
        }

        ring = GlowLine.Make(shield.transform, "BastionRing", 48, 0.1f, glow);
        ring.loop = true;
    }

    // a flattened, tapering slab of rock (facing +z, base at y=0) narrowing towards its peak
    static Vector3[][] SlabRings(float h, float hw, float hd)
    {
        const int sides = 6;
        float[] ys = { -0.15f, h * 0.55f, h * 0.85f };
        float[] ws = { 1f, 0.92f, 0.62f };
        float[] ds = { 1f, 0.9f, 0.7f };
        var rings = new Vector3[ys.Length][];
        for (int r = 0; r < ys.Length; r++)
        {
            rings[r] = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float ang = (j + Random.Range(-0.2f, 0.2f)) * Mathf.PI * 2f / sides;
                float jit = Random.Range(0.85f, 1.12f);
                float y = ys[r] + (r == 0 ? 0f : Random.Range(-0.08f, 0.08f));
                rings[r][j] = new Vector3(Mathf.Sin(ang) * hw * ws[r] * jit, y, Mathf.Cos(ang) * hd * ds[r] * jit);
            }
        }
        return rings;
    }

    // shield: a bullet struck a slab
    public void Impact(Vector3 point)
    {
        Vector3 away = point - transform.position; away.y = 0f;
        PowerFx.Sparks(point, color, 10, 6f, 0.35f, 0.07f, 1.5f, away.normalized, 100f);
        if (Random.value < 0.5f) RockDebris.Chunk(point, away.normalized * 3f + Vector3.up * 2f, 0.12f, 0.6f);
        PowerFx.Flash(point, color, 2.5f, 2.5f, 0.12f);
        Rumble.Play(gameObject, 0.15f, 0.3f, 0.06f, fade: false);
    }

    void Update()
    {
        if (!active) return;

        // dashed out of it, or time's up
        Vector3 moved = transform.position - plantedAt; moved.y = 0f;
        if (Time.time >= until || moved.magnitude > breakDistance) { End(true); return; }

        // rise, then track the aim
        float rise = Mathf.Clamp01((Time.time - started) / riseTime);
        rise = 1f - (1f - rise) * (1f - rise);
        Vector3 ground = shield.transform.position;
        ground = new Vector3(transform.position.x, ground.y, transform.position.z);
        shield.transform.position = ground;
        var target = Quaternion.LookRotation(AbilityKit.AimDir(gameObject));
        shield.transform.rotation = Quaternion.Slerp(shield.transform.rotation, target, turnSpeed * Time.deltaTime);

        for (int i = 0; i < slabList.Count; i++)
        {
            var s = slabList[i];
            if (s == null) continue;
            var p = s.localPosition;
            p.y = Mathf.Lerp(-slabHeights[i] - 0.05f, 0f, rise);
            s.localPosition = p;
        }

        // glow: pulses, and flickers warning in the last second
        float left = until - Time.time;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f);
        if (left < 1f) pulse *= 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(Time.time * 30f));
        foreach (var seam in seams) GlowLine.SetColor(seam, color, pulse * rise);

        Vector3 c = ground + Vector3.up * 0.06f;
        AbilityKit.Circle(ring, c, shieldRadius + 0.25f);
        GlowLine.SetColor(ring, color, 0.6f * pulse * rise);
    }

    void End(bool crumble)
    {
        if (!active) return;
        active = false;
        if (movement != null)
        {
            movement.knockbackMultiplier = savedKnockback;
            movement.ClearSpeedModifier("bastion");
        }
        if (shield == null) return;

        if (crumble)
        {
            foreach (var s in slabList)
                if (s != null)
                {
                    RockDebris.Burst(s.position, 5, 4f, 0.25f);
                    RockDebris.Dust(s.position - Vector3.up * 0.5f, 1.2f, 6, 0.3f);
                }
            CameraShake.Shake(0.12f, 0.15f);
        }
        Destroy(shield);
        shield = null;
        slabList.Clear(); slabHeights.Clear(); seams.Clear();
    }

    void OnDisable() => End(false);
}
