using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Granite Vow — Rune III: Rockslide. The wizard charges behind a rolling boulder,
/// rocks tumbling off it and dust boiling up behind, bowling over everyone in the way.
/// The boulder shatters into a spray of stone at the end of the charge.
public class RockslideAbility : MonoBehaviour, IActiveAbility
{
    public float distance = 7f;
    public float time = 0.35f;
    public float hitRadius = 1.4f;
    public float damage = 20f;
    public float knockback = 18f;
    public float stunDuration = 0.6f;

    [Header("Look")]
    public float boulderSize = 1.5f;

    public void Activate(GameObject caster) => StartCoroutine(Charge(caster));

    IEnumerator Charge(GameObject caster)
    {
        var cc = caster.GetComponent<CharacterController>();
        Vector3 dir = AbilityKit.MoveDir(caster);
        caster.transform.forward = dir;
        Color theme = AbilityKit.Theme(caster);
        var hit = new System.Collections.Generic.HashSet<GameObject>();
        float speed = distance / time;

        // the ground cracks open and a boulder heaves up in front of the wizard
        Vector3 start = AbilityKit.Ground(caster.transform.position + Vector3.up);
        RockDebris.Burst(start, 8, 5f, 0.3f, -dir * 1.5f);
        RockDebris.Dust(start, 2f, 14);
        var boulder = MakeBoulder();
        CameraShake.Shake(0.25f, time + 0.15f);
        Rumble.Play(caster, 0.9f, 0.4f, time + 0.1f);

        float nextDebris = 0f, nextDust = 0f;
        Vector3 spinAxis = Vector3.Cross(Vector3.up, dir);
        for (float t = 0; t < time && !PlayerHealthControl.IsIncapacitated(caster.transform); t += Time.deltaTime)
        {
            Vector3 step = dir * speed * Time.deltaTime;
            if (cc != null && cc.enabled) cc.Move(step); else caster.transform.position += step;

            // boulder rolls ahead, grows in as it forms
            float grow = Mathf.Clamp01(t / 0.08f);
            boulder.transform.position = caster.transform.position + dir * 0.9f + Vector3.up * (boulderSize * 0.5f * grow);
            boulder.transform.localScale = Vector3.one * boulderSize * grow;
            boulder.transform.Rotate(spinAxis, speed / (boulderSize * 0.5f) * Mathf.Rad2Deg * Time.deltaTime, Space.World);

            // rocks tumbling off the sides and back, and a boiling dust trail
            Vector3 feet = caster.transform.position;
            if (Time.time >= nextDebris)
            {
                nextDebris = Time.time + 0.025f;
                Vector3 side = spinAxis * Random.Range(-1f, 1f);
                RockDebris.Chunk(feet + dir * 0.6f + side * 0.6f + Vector3.up * 0.4f,
                                 side * 4f - dir * Random.Range(1f, 4f) + Vector3.up * Random.Range(3f, 6f),
                                 Random.Range(0.15f, 0.35f), 0.9f);
            }
            if (Time.time >= nextDust)
            {
                nextDust = Time.time + 0.06f;
                RockDebris.Dust(feet, 1.2f, 5, 0.4f);
            }

            foreach (var e in AbilityKit.Enemies(caster.transform.position + Vector3.up, hitRadius, caster))
            {
                if (!hit.Add(e)) continue;
                DamageEvents.Deal(e, damage, caster);
                AbilityKit.Knockback(e, (dir + Vector3.up * 0.1f) * knockback);
                ElementReactions.AbilityHit(e, caster, damage, heavy: true);
                StatusEffects.Of(e).Stun(0.3f, stunDuration);
                // stone bursts off them where the boulder hits
                RockDebris.Burst(e.transform.position + Vector3.up * 0.5f, 7, 6f, 0.3f, dir * 3f);
                RockDebris.Dust(e.transform.position, 1.5f, 8);
                AbilityKit.Shockwave(e.transform.position, 1.8f, theme, 0.3f);
                CameraShake.Shake(0.3f, 0.15f);
            }
            yield return null;
        }

        // the boulder smashes apart at the end of the run
        Vector3 end = boulder.transform.position;
        Destroy(boulder);
        RockDebris.Burst(AbilityKit.Ground(end + Vector3.up), 16, 8f, 0.4f, dir * 3f);
        RockDebris.Dust(AbilityKit.Ground(end + Vector3.up), 3f, 20);
        AbilityKit.Shockwave(caster.transform.position, 2.4f, theme, 0.35f);
        CameraShake.Shake(0.3f, 0.2f);
    }

    GameObject MakeBoulder()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "RockslideBoulder";
        Destroy(go.GetComponent<Collider>()); // hits are handled by the charge, not physics
        go.GetComponent<MeshRenderer>().sharedMaterial = RockDebris.StoneMaterial(Color.gray);
        go.transform.localScale = Vector3.zero;
        // a few lumps so it doesn't look like a perfect ball
        for (int i = 0; i < 5; i++)
        {
            var lump = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(lump.GetComponent<Collider>());
            lump.transform.SetParent(go.transform, false);
            lump.transform.localPosition = Random.onUnitSphere * 0.38f;
            lump.transform.localRotation = Random.rotation;
            lump.transform.localScale = Vector3.one * Random.Range(0.3f, 0.45f);
            lump.GetComponent<MeshRenderer>().sharedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
        }
        return go;
    }
}
