using System.Collections.Generic;
using UnityEngine;

/// A jagged ice crystal that closes around a frozen enemy, then shatters
public class IceEncase : MonoBehaviour
{
    GameObject target;
    float until, born;
    Color ice;
    WizardData wizard;
    readonly List<Transform> shards = new();
    readonly List<Vector3> fullScale = new();

    public void Init(GameObject t, float duration, int stacks, Color color, WizardData wiz)
    {
        target = t; born = Time.time; until = Time.time + duration; ice = color; wizard = wiz;
        transform.position = t.transform.position;

        // more counters = a bigger, thicker crystal
        int count = 6 + stacks * 2;
        float size = 0.8f + 0.1f * stacks;
        for (int i = 0; i < count; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(transform, false);
            float ang = i / (float)count * 360f + Random.Range(-15f, 15f);
            Vector3 radial = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            s.transform.localPosition = radial * Random.Range(0.25f, 0.45f) * size + Vector3.up * Random.Range(0.3f, 1.3f);
            // long crystals leaning outward
            s.transform.localRotation = Quaternion.LookRotation(radial) * Quaternion.Euler(Random.Range(-35f, 10f), 0f, Random.Range(-20f, 20f));
            Vector3 sc = new Vector3(Random.Range(0.18f, 0.35f), Random.Range(0.9f, 1.8f), Random.Range(0.18f, 0.35f)) * size;
            s.transform.localScale = Vector3.zero;
            var r = s.GetComponent<MeshRenderer>();
            r.sharedMaterial = PowerFx.IceMaterial();
            PowerFx.Tint(r, new Color(ice.r, ice.g, ice.b, Random.Range(0.45f, 0.7f)));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shards.Add(s.transform);
            fullScale.Add(sc);
        }
        PowerFx.Prefab(wizard != null ? wizard.passiveEffectPrefab : null, t.transform.position + Vector3.up, 2f);
        PowerFx.Sparks(t.transform.position + Vector3.up, Color.white, 16, 4f, 0.4f, 0.06f, 0.5f);
        PowerFx.Flash(t.transform.position + Vector3.up, ice, 5f, 4f, 0.3f);
        Rumble.Play(t, 0.5f, 0.8f, 0.25f);
    }

    void Update()
    {
        if (target == null || !target.activeInHierarchy || Time.time >= until) { Shatter(); return; }
        transform.position = target.transform.position;

        // crystals snap shut, then shiver slightly
        float grow = Mathf.Clamp01((Time.time - born) / 0.12f);
        float shiver = until - Time.time < 0.3f ? 0.04f : 0.01f;
        for (int i = 0; i < shards.Count; i++)
            shards[i].localScale = fullScale[i] * grow * (1f + Random.Range(-shiver, shiver));
    }

    void Shatter()
    {
        Vector3 at = transform.position + Vector3.up;
        PowerFx.IceShards(at, ice, 10 + shards.Count, 6f, 0.2f);
        PowerFx.Sparks(at, Color.white, 20, 6f, 0.4f, 0.06f, 1f);
        PowerFx.Puffs(at, new Color(0.85f, 0.95f, 1f, 1f), 6, 2f, 0.6f, 0.6f, additive: true);
        CameraShake.Shake(0.1f, 0.1f);
        Destroy(gameObject);
    }
}
