using System.Collections.Generic;
using UnityEngine;

/// The planted state + stone shield for Bastion Stance (lives on the player)
public class BastionStance : MonoBehaviour
{
    public float shieldRadius = 1.4f;
    public float arcDegrees = 115f;
    public int slabs = 5;
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
    readonly List<LineRenderer> seams = new();
    LineRenderer ring, crest;

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

        slabList.Clear(); seams.Clear();
        for (int i = 0; i < slabs; i++)
        {
            float f = slabs == 1 ? 0.5f : i / (float)(slabs - 1);
            float ang = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, f);
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "BastionSlab";
            slab.transform.SetParent(shield.transform, false);
            slab.transform.localPosition = dir * shieldRadius + Vector3.up * (-slabHeight * 0.5f); // starts buried
            slab.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-5f, 5f));
            float h = slabHeight * Random.Range(0.85f, 1.1f);
            slab.transform.localScale = new Vector3(0.78f, h, 0.4f);
            slab.GetComponent<MeshRenderer>().sharedMaterial = stone;
            slab.AddComponent<BastionShieldPiece>().Init(this);
            slabList.Add(slab.transform);

            // glowing rune seam down each slab's face
            var seam = GlowLine.Make(slab.transform, "Seam", 2, 0.07f, glow);
            seam.useWorldSpace = false;
            seam.SetPosition(0, new Vector3(0f, -0.35f, 0.52f));
            seam.SetPosition(1, new Vector3(0f, 0.4f, 0.52f));
            seams.Add(seam);
        }

        ring = GlowLine.Make(shield.transform, "BastionRing", 48, 0.1f, glow);
        ring.loop = true;
        crest = GlowLine.Make(shield.transform, "BastionCrest", 16, 0.12f, glow);
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

        foreach (var s in slabList)
        {
            if (s == null) continue;
            var p = s.localPosition;
            p.y = Mathf.Lerp(-s.localScale.y * 0.5f, s.localScale.y * 0.5f, rise);
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

        // a glowing crest along the top of the arc
        int n = crest.positionCount;
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            float ang = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, f);
            Vector3 d = shield.transform.rotation * (Quaternion.Euler(0f, ang, 0f) * Vector3.forward);
            crest.SetPosition(i, ground + d * (shieldRadius + 0.22f) + Vector3.up * (slabHeight * rise + 0.02f));
        }
        GlowLine.SetColor(crest, Color.Lerp(color, Color.white, 0.3f), pulse * rise);
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
        slabList.Clear(); seams.Clear();
    }

    void OnDisable() => End(false);
}
