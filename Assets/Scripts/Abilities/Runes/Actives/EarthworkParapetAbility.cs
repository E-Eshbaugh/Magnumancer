using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Granite Vow — Rune II: Earthwork Parapet. A jagged crag of rock grinds up out of the
/// ground right underneath you, lifting you onto its crown (shoving back anyone standing
/// close). It holds for a few seconds — you shoot down from the high ground and the rock
/// is cover — then it sinks back into the earth. Every crag is shaped a little differently.
public class EarthworkParapetAbility : MonoBehaviour, IActiveAbility
{
    public float height = 2.2f;
    public float topRadius = 1.3f;
    public float riseTime = 0.45f;
    public float holdTime = 3.5f;
    public float sinkTime = 0.6f;
    [Tooltip("How far the crag grinds round as it rises")]
    public float twistDegrees = 50f;

    [Header("Eruption")]
    public float shoveRadius = 2.4f;
    public float shoveForce = 14f;
    public float shoveDamage = 10f;
    GameObject activeTower;

    /// The tower is up (Highground: forged shots from on top kick rubble)
    public bool TowerUp => activeTower != null;

    public void Activate(GameObject caster) => StartCoroutine(Raise(caster));

    IEnumerator Raise(GameObject caster)
    {
        var movement = caster.GetComponent<PlayerMovement3D>();
        var cc = caster.GetComponent<CharacterController>();
        Vector3 ground = AbilityKit.Ground(caster.transform.position + Vector3.up);
        Color theme = AbilityKit.Theme(caster);

        var tower = Build(ground);
        activeTower = tower;
        Destroy(tower, riseTime + holdTime + sinkTime + 0.2f);
        Vector3 buried = ground - Vector3.up * (height + 0.2f);
        tower.transform.position = buried;

        // the ground erupts: shove people off the spot, chunks and dust fly
        foreach (var e in AbilityKit.Enemies(ground, shoveRadius, caster))
        {
            Vector3 away = e.transform.position - ground; away.y = 0f;
            AbilityKit.Knockback(e, (away.sqrMagnitude > 0.01f ? away.normalized : Random.onUnitSphere) * shoveForce);
            DamageEvents.Deal(e, shoveDamage, caster);
        }
        RockDebris.Burst(ground, 10, 7f, 0.35f);
        RockDebris.Dust(ground, 2.5f, 18);
        AbilityKit.Shockwave(ground, shoveRadius + 0.5f, theme, 0.35f);
        CameraShake.Shake(0.25f, riseTime + 0.1f);
        Rumble.Play(caster, 0.8f, 0.5f, riseTime);

        // 1) grind up out of the earth, carrying the caster
        float lastTop = ground.y;
        for (float t = 0; t < riseTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / riseTime);
            float topY = ground.y + height * k;
            if (OnTop(caster, ground) && cc != null && cc.enabled)
                cc.Move(Vector3.up * Mathf.Max(0f, topY - lastTop));   // lift them before the stone arrives
            lastTop = topY;
            tower.transform.position = Vector3.Lerp(buried, ground, k);
            tower.transform.rotation = Quaternion.Euler(0f, -twistDegrees * (1f - k), 0f);
            if (Random.value < 0.4f) RockDebris.Chunk(ground + Random.insideUnitSphere * 0.8f + Vector3.up * 0.2f,
                                                       Random.onUnitSphere * 2f + Vector3.up * 4f, 0.2f, 0.8f);
            SetElevation(movement, caster, ground);
            yield return null;
        }
        tower.transform.SetPositionAndRotation(ground, Quaternion.identity);

        // 2) hold the high ground
        for (float t = 0; t < holdTime; t += Time.deltaTime)
        {
            SetElevation(movement, caster, ground);
            yield return null;
        }

        // 3) sink back into the earth (the wizard drops down with it)
        RockDebris.Dust(ground, 2f, 14, 0.3f);
        for (float t = 0; t < sinkTime; t += Time.deltaTime)
        {
            float k = t / sinkTime;
            tower.transform.position = Vector3.Lerp(ground, buried, k * k);
            tower.transform.rotation = Quaternion.Euler(0f, twistDegrees * 0.4f * k * k, 0f);
            SetElevation(movement, caster, ground);
            yield return null;
        }
        if (movement != null) movement.elevation = 0f;
        Destroy(tower);
        activeTower = null;
    }

    void OnDisable()
    {
        // caster died mid-ability: don't leave their shots angled down
        var m = GetComponentInParent<PlayerMovement3D>();
        if (m != null) m.elevation = 0f;
        if (activeTower != null) Destroy(activeTower);
    }

    bool OnTop(GameObject caster, Vector3 ground)
    {
        Vector3 d = caster.transform.position - ground; d.y = 0f;
        return d.magnitude <= topRadius + 0.2f;
    }

    // how high above the arena floor the caster stands (drives high-ground aiming)
    void SetElevation(PlayerMovement3D movement, GameObject caster, Vector3 ground)
    {
        if (movement == null) return;
        movement.elevation = OnTop(caster, ground) ? Mathf.Max(0f, caster.transform.position.y - ground.y) : 0f;
    }

    GameObject Build(Vector3 ground)
    {
        var root = new GameObject("EarthworkParapet");
        root.transform.position = ground;
        var stone = RockDebris.StoneMaterial(Color.gray);
        var owned = root.AddComponent<OwnedMeshes>();

        // the crag itself: a jagged, leaning rock with a roughly flat crown to stand on.
        // Solid (convex collider) — it's cover, and the crown is the high ground.
        var crag = Rock(root, owned, "Crag", CragRings(), new Vector3(0f, height, 0f), stone, Vector3.zero, Quaternion.identity);
        var mc = crag.AddComponent<MeshCollider>();
        mc.sharedMesh = crag.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;
        AbilityKit.BlockNavigation(mc);

        // shards jutting from its flanks, leaning away as if the rock split while it heaved up
        // (visual only, so they never snag anyone or crowd the crown)
        int shards = Random.Range(3, 5);
        float spin = Random.value * 360f;
        for (int i = 0; i < shards; i++)
        {
            float ang = spin + i * 360f / shards + Random.Range(-25f, 25f);
            Vector3 radial = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            float h = height * (i == 0 ? Random.Range(0.7f, 0.85f) : Random.Range(0.3f, 0.55f));
            float w = Random.Range(0.35f, 0.5f);
            var tilt = Quaternion.AngleAxis(Random.Range(14f, 30f), Vector3.Cross(Vector3.up, radial));
            Rock(root, owned, "Shard", SpireRings(h, w),
                 new Vector3(Random.Range(-0.1f, 0.1f), h + w * 0.6f, Random.Range(-0.1f, 0.1f)),
                 i % 2 == 0 ? DarkStone() : stone, radial * Random.Range(1.0f, 1.25f), tilt);
        }

        // tumbled boulders at its foot
        int boulders = Random.Range(3, 6);
        for (int i = 0; i < boulders; i++)
        {
            Vector3 radial = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            float s = Random.Range(0.22f, 0.4f);
            Rock(root, owned, "Boulder", SpireRings(s * 0.8f, s, 6), new Vector3(0f, s, 0f),
                 Random.value < 0.5f ? DarkStone() : stone, radial * Random.Range(1.45f, 1.9f),
                 Quaternion.Euler(Random.Range(-15f, 15f), Random.value * 360f, Random.Range(-15f, 15f)));
        }

        // moss creeping over the crown's edge, and sometimes a ledge lower down
        int patches = Random.Range(2, 4);
        for (int i = 0; i < patches; i++)
        {
            Vector3 radial = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            bool ledge = i > 0 && Random.value < 0.5f;
            float y = ledge ? height * Random.Range(0.45f, 0.7f) : height - 0.04f;
            float r = ledge ? 1.3f : topRadius * Random.Range(0.75f, 0.9f);
            float s = Random.Range(0.3f, 0.5f);
            Rock(root, owned, "Moss", SpireRings(0.06f, s, 7), new Vector3(0f, 0.09f, 0f), Moss(),
                 radial * r + Vector3.up * y, Quaternion.Euler(Random.Range(-8f, 8f), Random.value * 360f, Random.Range(-8f, 8f)));
        }
        return root;
    }

    // rings of the main crag, from the buried base up to the rim of its crown
    Vector3[][] CragRings()
    {
        const int sides = 8;
        float[] ys = { -0.35f, height * 0.25f, height * 0.55f, height * 0.82f, height - 0.06f };
        float[] rs = { 1.65f, 1.45f, 1.3f, 1.35f, topRadius };
        Vector3 lean = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(0.1f, 0.22f);
        var rings = new Vector3[ys.Length][];
        for (int r = 0; r < ys.Length; r++)
        {
            bool crown = r == ys.Length - 1;
            // bulge the middle out to one side but bring the crown back over the base,
            // so the standing spot stays centred on the caster
            Vector3 centre = lean * Mathf.Sin(Mathf.Clamp01(ys[r] / height) * Mathf.PI);
            rings[r] = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float ang = (j + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / sides;
                float rad = rs[r] * (crown ? Random.Range(0.92f, 1.08f) : Random.Range(0.78f, 1.12f));
                if (!crown && r > 0 && Random.value < 0.18f) rad *= 1.3f;   // the odd knob of rock sticking out
                float y = ys[r] + (crown ? -Random.Range(0f, 0.1f) : Random.Range(-0.15f, 0.15f));
                rings[r][j] = centre + new Vector3(Mathf.Sin(ang) * rad, y, Mathf.Cos(ang) * rad);
            }
        }
        return rings;
    }

    // a smaller tapering rock: wide base narrowing to a jagged tip (or a squat lump when short)
    static Vector3[][] SpireRings(float h, float w, int sides = 5)
    {
        float[] ys = { -0.25f, h * 0.45f, h * 0.85f };
        float[] rs = { w, w * 0.7f, w * 0.35f };
        var rings = new Vector3[ys.Length][];
        for (int r = 0; r < ys.Length; r++)
        {
            rings[r] = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float ang = (j + Random.Range(-0.25f, 0.25f)) * Mathf.PI * 2f / sides;
                float rad = rs[r] * Random.Range(0.75f, 1.15f);
                rings[r][j] = new Vector3(Mathf.Sin(ang) * rad, ys[r] + Random.Range(-0.05f, 0.05f) * (h + w), Mathf.Cos(ang) * rad);
            }
        }
        return rings;
    }

    static GameObject Rock(GameObject root, OwnedMeshes owned, string name, Vector3[][] rings, Vector3 cap,
                           Material mat, Vector3 localPos, Quaternion rot)
        => RockShapes.Spawn(root.transform, owned, name, rings, cap, mat, localPos, rot);

    static Material DarkStone() => RockShapes.DarkStone();
    static Material Moss() => RockShapes.Moss();
}
