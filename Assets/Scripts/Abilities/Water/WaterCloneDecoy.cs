using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a Shadow Clone pass for its caster: it holds a copy of whatever gun the caster
/// is holding (aimed the mirrored way, like the clone's movement), moves at the caster's
/// current speed, and pops in a splash when its time is up or the caster goes down.
/// </summary>
public class WaterCloneDecoy : MonoBehaviour
{
    public static readonly HashSet<WaterCloneDecoy> Active = new();
    public bool CanLure => !popped && caster != null && caster.activeInHierarchy && Time.time < expiresAt
                           && (casterHealth == null || casterHealth.IsStanding);
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    GameObject caster;
    PlayerMovement3D casterMove;
    PlayerHealthControl casterHealth;
    GunSwapControl casterGuns;
    CloneMovement cloneMove;
    Color tint;
    float expiresAt;
    bool popped;

    // A committed zombie attack consumes the decoy instead of hurting its caster.
    public void HitByMonster() { if (CanLure) Pop(); }

    // decoy gun: mesh-only copy of the caster's held gun (no gun scripts run on it)
    GameObject gunSource;
    Transform gunCopy;

    // the clone's movement is the caster's stick read without the iso turn and with Z flipped
    static readonly Quaternion InverseIso = Quaternion.Inverse(Quaternion.Euler(0f, 45f, 0f));

    public void Setup(GameObject caster, float duration, Color tint)
    {
        this.caster = caster;
        this.tint = tint;
        casterMove = caster.GetComponent<PlayerMovement3D>();
        casterHealth = caster.GetComponent<PlayerHealthControl>();
        casterGuns = caster.GetComponentInChildren<GunSwapControl>();
        cloneMove = GetComponent<CloneMovement>();
        expiresAt = Time.time + duration;

        if (cloneMove != null && casterMove != null)
        {
            cloneMove.gravity = casterMove.gravity;
            cloneMove.jumpForce = casterMove.jumpForce;
            cloneMove.dashSpeed = casterMove.dashSpeed;
            cloneMove.dashTime = casterMove.dashDuration;
        }
    }

    void Update()
    {
        bool casterGone = caster == null || !caster.activeInHierarchy || (casterHealth != null && !casterHealth.IsStanding);
        if (casterGone || Time.time >= expiresAt)
        {
            Pop();
            return;
        }

        // keep pace with the caster's buffs, slows and gun weight
        if (cloneMove != null && casterMove != null)
            cloneMove.moveSpeed = casterMove.currentMoveSpeed * casterMove.moveSpeedMultiplier;
    }

    void LateUpdate()
    {
        if (caster == null) return;

        var held = casterGuns != null ? casterGuns.CurrentGun : null;
        if (held != gunSource) RebuildGun(held);
        if (gunCopy == null || gunSource == null) return;

        // same offset and aim as the caster's gun, mirrored the way the clone's movement is
        Transform src = gunSource.transform;
        Vector3 offset = Mirror(src.position - caster.transform.position);
        gunCopy.SetPositionAndRotation(transform.position + offset,
            Quaternion.LookRotation(Mirror(src.forward), Mirror(src.up)));
    }

    static Vector3 Mirror(Vector3 v)
    {
        v = InverseIso * v;
        v.z = -v.z;
        return v;
    }

    void RebuildGun(GameObject held)
    {
        if (gunCopy != null) Destroy(gunCopy.gameObject);
        gunCopy = null;
        gunSource = held;
        if (held == null) return;

        Transform src = held.transform;
        var root = new GameObject("DecoyGun").transform;
        root.SetParent(transform, false);
        root.localScale = Div(src.lossyScale, transform.lossyScale);

        var parts = new List<MeshRenderer>();
        held.GetComponentsInChildren(parts);
        foreach (var part in parts)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !part.enabled) continue;

            var go = new GameObject(part.name);
            go.layer = part.gameObject.layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = src.InverseTransformPoint(part.transform.position);
            go.transform.localRotation = Quaternion.Inverse(src.rotation) * part.transform.rotation;
            go.transform.localScale = Div(part.transform.lossyScale, src.lossyScale);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = part.sharedMaterials;
            r.shadowCastingMode = part.shadowCastingMode;
        }
        gunCopy = root;
    }

    static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(
        Mathf.Abs(b.x) > 1e-5f ? a.x / b.x : a.x,
        Mathf.Abs(b.y) > 1e-5f ? a.y / b.y : a.y,
        Mathf.Abs(b.z) > 1e-5f ? a.z / b.z : a.z);

    void Pop()
    {
        if (popped) return;
        popped = true;
        Active.Remove(this);
        WaterCloneBurst.Play(transform.position, tint, 0.6f);
        Destroy(gameObject);
    }
}
