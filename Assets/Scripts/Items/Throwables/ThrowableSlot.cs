using UnityEngine;

/// <summary>
/// The throwable a player is carrying (one at a time; a new pickup replaces it). A small
/// copy of it floats over their head. LB throws it along their aim.
/// </summary>
public class ThrowableSlot : MonoBehaviour
{
    ItemBook.Def held;
    Transform icon;
    PlayerMovement3D movement;

    public ItemBook.Def Held => held;

    public static void Give(GameObject player, ItemBook.Def def)
    {
        var slot = player.GetComponent<ThrowableSlot>();
        if (slot == null) slot = player.AddComponent<ThrowableSlot>();
        slot.Hold(def);
    }

    void Hold(ItemBook.Def def)
    {
        held = def;
        if (movement == null) movement = GetComponent<PlayerMovement3D>();
        if (icon != null) Destroy(icon.gameObject);
        icon = new GameObject("HeldThrowable").transform;
        icon.SetParent(transform, false);
        ItemVisuals.Model(def.id, icon);
        icon.localScale = Vector3.one * 0.55f / ParentScale();
    }

    void Update()
    {
        if (held == null) return;

        // hovers over the shoulder, turning slowly
        var cam = Camera.main;
        Vector3 side = cam != null ? cam.transform.right : Vector3.right;
        icon.position = transform.position + Vector3.up * (2.45f + 0.06f * Mathf.Sin(Time.time * 3f)) + side * 0.45f;
        icon.rotation = Quaternion.Euler(0f, Time.time * 120f, 0f);

        var pad = movement != null ? movement.gamepad : null;
        if (pad == null || GamePause.InputBlocked || PlayerHealthControl.IsIncapacitated(this) || WizardSpawnEffect.IsArriving(transform)) return;
        if (pad.leftShoulder.wasPressedThisFrame) Throw();
    }

    void Throw()
    {
        var def = held;
        held = null;
        if (icon != null) Destroy(icon.gameObject);
        ThrownItem.Launch(def, gameObject);
        Rumble.Play(gameObject, 0.2f, 0.4f, 0.12f);
    }

    float ParentScale()
    {
        var s = transform.lossyScale;
        return Mathf.Max(0.01f, (s.x + s.y + s.z) / 3f);
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) Destroy(this); }   // eliminated: the player switches off

    void OnDestroy()
    {
        if (icon != null) Destroy(icon.gameObject);
    }
}
