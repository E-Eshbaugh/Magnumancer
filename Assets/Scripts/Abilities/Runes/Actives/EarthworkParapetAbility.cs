using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Granite Vow — Rune II: Earthwork Parapet. A spiralling stone tower corkscrews up out of
/// the ground right underneath you, lifting you onto its top (shoving back anyone standing
/// close). It holds for a few seconds — you shoot down from the high ground and its
/// column is cover — then it sinks back into the earth.
public class EarthworkParapetAbility : MonoBehaviour, IActiveAbility
{
    public float height = 2.2f;
    public float topRadius = 1.3f;
    public float riseTime = 0.45f;
    public float holdTime = 3.5f;
    public float sinkTime = 0.6f;
    [Tooltip("How far the tower twists as it rises")]
    public float twistDegrees = 220f;
    public int spiralSteps = 10;

    [Header("Eruption")]
    public float shoveRadius = 2.4f;
    public float shoveForce = 14f;
    public float shoveDamage = 10f;

    public void Activate(GameObject caster) => StartCoroutine(Raise(caster));

    IEnumerator Raise(GameObject caster)
    {
        var movement = caster.GetComponent<PlayerMovement3D>();
        var cc = caster.GetComponent<CharacterController>();
        Vector3 ground = AbilityKit.Ground(caster.transform.position + Vector3.up);
        Color theme = AbilityKit.Theme(caster);

        var tower = Build(ground);
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

        // 1) corkscrew up, carrying the caster
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
    }

    void OnDisable()
    {
        // caster died mid-ability: don't leave their shots angled down
        var m = GetComponentInParent<PlayerMovement3D>();
        if (m != null) m.elevation = 0f;
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
        var mat = RockDebris.StoneMaterial(Color.gray);

        // central column (solid: it's cover)
        var column = Part(root, PrimitiveType.Cylinder, mat, new Vector3(0f, height * 0.5f, 0f),
                          new Vector3(1.5f, height * 0.5f, 1.5f), Quaternion.identity);

        // flat top to stand on (convex mesh collider, so it's round like it looks)
        var top = Part(root, PrimitiveType.Cylinder, mat, new Vector3(0f, height - 0.1f, 0f),
                       new Vector3(topRadius * 2f, 0.12f, topRadius * 2f), Quaternion.identity);
        Destroy(top.GetComponent<Collider>());
        var mc = top.AddComponent<MeshCollider>();
        mc.sharedMesh = top.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;

        // the spiral: slabs winding up around the column (visual only)
        for (int i = 0; i < spiralSteps; i++)
        {
            float f = (i + 0.5f) / spiralSteps;
            float ang = f * 360f * 1.25f;
            Vector3 radial = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            var slab = Part(root, PrimitiveType.Cube, mat,
                            radial * 0.95f + Vector3.up * (height * f - 0.1f),
                            new Vector3(0.75f, 0.28f, 0.55f),
                            Quaternion.LookRotation(Vector3.Cross(Vector3.up, radial), Vector3.up) * Quaternion.Euler(0f, 0f, -12f));
            Destroy(slab.GetComponent<Collider>());
        }
        return root;
    }

    static GameObject Part(GameObject root, PrimitiveType type, Material mat, Vector3 localPos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
