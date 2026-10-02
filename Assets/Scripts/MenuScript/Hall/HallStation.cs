using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player's spot in the Great Hall: a rune dais where their wizard stands once they
/// join (X). Each player picks on their own pad, all at once:
///  • Wizard — LB/RB (or left stick) wizard, d-pad ▲▼ active rune, ◄► passive rune, Y lore;
///  • Armory — LB/RB tier, stick browses (the gun appears in the wizard's hand), d-pad
///    slots it: the gun flies to that side of the wizard (up/right/down/left), X+d-pad clears;
///  • Ready — the wizard cheers and their guns orbit them like in a match.
/// A moves forward, B goes back (B on the wizard step leaves the hall).
/// </summary>
public class HallStation : MonoBehaviour
{
    public enum Stage { Empty, Wizard, Armory, Ready }

    [Tooltip("0-3: corner of the screen this station's card sits in (TL, TR, BL, BR)")]
    public int index;
    [Tooltip("Optional banner in this player's colour (pivot at its top), unfurled when they join")]
    public Transform banner;

    [Header("Layout")]
    public float slotRadius = 1.75f;
    public float handGunLength = 1.35f;
    public float slotGunLength = 1.05f;
    public float stickRepeatDelay = 0.3f;
    public float stickRepeatRate = 0.16f;

    public Stage CurrentStage { get; private set; } = Stage.Empty;
    public Gamepad Pad { get; private set; }
    public bool Joined => CurrentStage != Stage.Empty;
    public bool IsReady => CurrentStage == Stage.Ready;
    public int LastChangeFrame { get; private set; } = -1;

    public int WizardIndex { get; private set; }
    public int ActiveRune { get; private set; }
    public int PassiveRune { get; private set; }
    public int TierIndex { get; private set; }
    public int WeaponIndex { get; private set; }
    public bool ShowLore { get; private set; }
    public readonly WeaponData[] Loadout = new WeaponData[4];

    public HallCatalog Catalog => catalog;
    public WizardData Wizard => catalog != null && catalog.wizards.Length > 0 ? catalog.wizards[Mathf.Clamp(WizardIndex, 0, catalog.wizards.Length - 1)] : null;
    public WeaponData[] TierList => catalog.Tier(TierIndex);
    public WeaponData Browsed { get { var l = TierList; return l != null && l.Length > 0 ? l[Mathf.Clamp(WeaponIndex, 0, l.Length - 1)] : null; } }
    public Color PlayerColor => catalog.PlayerColor(index);
    public Color Theme => GlowLine.Brighten(WizardShade.Shade(Wizard, ActiveRune));
    public int Budget => Wizard != null ? Wizard.loadoutOrbs : 0;
    public int Spent { get { int s = 0; foreach (var w in Loadout) if (w != null) s += w.orbCost; return s; } }
    public HallDirector Director => director;

    HallDirector director;
    HallCatalog catalog;
    HallCard card;

    GameObject model;
    Animator anim;
    Transform hand;
    Transform handGun;
    WeaponData handGunData;
    readonly Transform[] slotGuns = new Transform[4];
    readonly WeaponData[] slotGunData = new WeaponData[4];
    readonly LineRenderer[] sockets = new LineRenderer[4];
    LineRenderer ring, outer, ley;
    Light glow;
    float orbit;          // ready-stage orbit angle
    float nextStickAt;
    int stickDir;
    float flashUntil;
    string flashText;

    // ---------- setup ----------

    public void Init(HallDirector director, HallCatalog catalog, Transform cardCanvas, Vector3 tableEdge)
    {
        this.director = director;
        this.catalog = catalog;
        WizardIndex = Mathf.Clamp(index * 2, 0, Mathf.Max(0, catalog.wizards.Length - 1));

        // the dais: an inner rune ring in the wizard's colour, an outer one in the player's
        ring = HallFx.RuneRing(transform, "RuneRing", 1.25f, 0.07f, 8);
        ring.transform.localPosition = Vector3.up * 0.06f;
        outer = HallFx.Ring(transform, "PlayerRing", 1.55f, 0.05f, 64);
        outer.transform.localPosition = Vector3.up * 0.05f;
        for (int i = 0; i < 4; i++)
        {
            sockets[i] = HallFx.Ring(transform, $"Socket{i}", 0.42f, 0.035f, 24);
            sockets[i].transform.localPosition = SlotPos(i, 0f);
        }
        glow = HallFx.PointLight(transform, "DaisLight", new Vector3(0f, 2.2f, 0f), Color.white, 0f, 6f);

        // energy line along the floor to the War Table
        ley = GlowLine.Make(transform, "LeyLine", 24, 0.12f, HallFx.RingMaterial());
        ley.useWorldSpace = true;
        Vector3 from = transform.position + (tableEdge - transform.position).normalized * 1.6f + Vector3.up * 0.05f;
        Vector3 to = tableEdge + Vector3.up * 0.05f;
        for (int i = 0; i < 24; i++) ley.SetPosition(i, Vector3.Lerp(from, to, i / 23f));

        if (banner != null) banner.localScale = new Vector3(1f, 0.12f, 1f);

        card = new HallCard(cardCanvas, this);
        Refresh();
    }

    /// Where loadout slot i hangs (0 up, 1 right, 2 down, 3 left — screen directions)
    Vector3 SlotPos(int i, float orbitDeg)
    {
        Vector3 dir = i switch { 0 => HallCamera.GroundUp, 1 => HallCamera.GroundRight, 2 => -HallCamera.GroundUp, _ => -HallCamera.GroundRight };
        dir = Quaternion.Euler(0f, orbitDeg, 0f) * dir;
        float h = i switch { 0 => 2.75f, 2 => 0.55f, _ => 1.35f };
        if (orbitDeg != 0f) h = 1.45f + 0.25f * Mathf.Sin((orbitDeg + i * 90f) * Mathf.Deg2Rad * 2f);
        float r = i == 2 && orbitDeg == 0f ? slotRadius * 0.85f : slotRadius;
        return dir * r + Vector3.up * h;
    }

    // ---------- join / leave ----------

    public void Join(Gamepad pad, int suggestedWizard)
    {
        Pad = pad;
        if (suggestedWizard >= 0) WizardIndex = suggestedWizard;
        ActiveRune = PassiveRune = 0;
        TierIndex = WeaponIndex = 0;
        for (int i = 0; i < 4; i++) Loadout[i] = null;
        ShowLore = false;
        SetStage(Stage.Wizard);
        SpawnModel(arrive: true);
        Rumble.Play(pad, 0.5f, 1f, 0.3f);
    }

    /// Back from a match: same pad, same picks, already standing ready
    public void Restore(Gamepad pad, int wizard, int active, int passive, WeaponData[] loadout, float arrivalDelay)
    {
        Pad = pad;
        WizardIndex = Mathf.Clamp(wizard, 0, catalog.wizards.Length - 1);
        ActiveRune = active; PassiveRune = passive;
        for (int i = 0; i < 4; i++)
            Loadout[i] = loadout != null && i < loadout.Length && loadout[i] != null && loadout[i] != catalog.placeholder ? loadout[i] : null;
        SetStage(Stage.Ready);
        SpawnModel(arrive: true, arrivalDelay);
        for (int i = 0; i < 4; i++) SetSlotGun(i, Loadout[i], fly: false);
        if (anim != null) anim.Play("ReadyIdle", 0, Random.value);
    }

    public void Leave()
    {
        if (model != null)
        {
            AbilityKit.Shockwave(transform.position, 1.6f, Theme, 0.35f);
            Destroy(model);
        }
        model = null; anim = null; hand = null;
        ClearHandGun();
        for (int i = 0; i < 4; i++) { Loadout[i] = null; SetSlotGun(i, null, fly: false); }
        Pad = null;
        SetStage(Stage.Empty);
        Ui(SfxId.UiBack);
    }

    void SetStage(Stage s)
    {
        CurrentStage = s;
        LastChangeFrame = Time.frameCount;
        if (s != Stage.Armory) ClearHandGun();
        if (s == Stage.Armory) ShowHandGun();
        Refresh();
    }

    // ---------- the wizard ----------

    void SpawnModel(bool arrive, float delay = 0f)
    {
        if (model != null) Destroy(model);
        if (catalog.wizardModel == null) return;
        model = Instantiate(catalog.wizardModel, transform);
        model.name = "Wizard";
        model.transform.localPosition = Vector3.zero;
        model.transform.rotation = HallCamera.FacingCamera * Quaternion.Euler(0f, -18f, 0f);
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        anim = model.GetComponentInChildren<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = catalog.wizardAnimator;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        hand = FindBone(model.transform, "handslot.r") ?? FindBone(model.transform, "hand.r");
        ApplyLook();
        if (arrive)
        {
            WizardSpawnEffect.Play(model, Wizard, delay);
            StartCoroutine(PlayAfter(delay + 0.25f, "Arrive"));
        }
    }

    static Transform FindBone(Transform t, string name)
    {
        if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return t;
        foreach (Transform c in t)
        {
            var f = FindBone(c, name);
            if (f != null) return f;
        }
        return null;
    }

    IEnumerator PlayAfter(float delay, string state)
    {
        yield return new WaitForSeconds(delay);
        Play(state);
    }

    void Play(string state, float fade = 0.12f)
    {
        if (anim != null && anim.runtimeAnimatorController != null) anim.CrossFadeInFixedTime(state, fade);
    }

    /// Robes in the active rune's shade, plus the shade the arrival bolt and rings read
    void ApplyLook()
    {
        if (model == null) return;
        HallFx.Dress(model, Wizard, ActiveRune);
        var s = WizardShade.Apply(model, Wizard, ActiveRune, null);
        s.color = WizardShade.Shade(Wizard, ActiveRune);
    }

    void ChangeWizard(int dir)
    {
        int n = catalog.wizards.Length;
        WizardIndex = (WizardIndex + dir + n) % n;
        ActiveRune = PassiveRune = 0;
        ApplyLook();
        Burst(big: true);
        Play("Swap");
        Rumble.Swap(Pad);
        Ui(SfxId.UiMove);
        Sfx.Play(SfxId.Blink, transform.position, 0.5f);
        Refresh();
    }

    void Burst(bool big)
    {
        var c = Theme;
        AbilityKit.Shockwave(transform.position, big ? 2.2f : 1.5f, c, big ? 0.45f : 0.3f);
        if (big && Wizard != null && Wizard.spawnEffectPrefab != null)
        {
            var fx = Instantiate(Wizard.spawnEffectPrefab, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(fx, 3f);
        }
        flare = big ? 1f : 0.6f;
    }
    float flare;

    // ---------- guns ----------

    void ShowHandGun()
    {
        var w = Browsed;
        if (w == handGunData && handGun != null) return;
        ClearHandGun();
        if (w == null || w.prefab == null) return;
        handGunData = w;
        handGun = HallFx.GunDisplay(w.prefab, handGunLength, transform);
        handGun.name = "HandGun";
        PlaceHandGun(1f);
    }

    void ClearHandGun()
    {
        if (handGun != null) Destroy(handGun.gameObject);
        handGun = null;
        handGunData = null;
    }

    void PlaceHandGun(float snap)
    {
        if (handGun == null) return;
        Vector3 grip = hand != null ? hand.position : transform.position + Vector3.up * 1.3f + HallCamera.GroundRight * 0.6f;
        // barrel out to screen-right and a little toward the camera, held up proudly
        Vector3 barrel = (HallCamera.GroundRight * 0.85f - HallCamera.GroundUp * 0.5f + Vector3.up * 0.25f).normalized;
        var rot = Quaternion.LookRotation(barrel) * Quaternion.Euler(0f, -90f, 0f);
        rot = Quaternion.AngleAxis(Mathf.Sin(Time.time * 1.7f) * 4f, barrel) * rot;
        Vector3 pos = grip + barrel * handGunLength * 0.18f;
        handGun.position = snap >= 1f ? pos : Vector3.Lerp(handGun.position, pos, snap);
        handGun.rotation = rot;
    }

    void SetSlotGun(int slot, WeaponData w, bool fly)
    {
        if (slotGunData[slot] == w && (w == null || slotGuns[slot] != null)) return;
        if (slotGuns[slot] != null) Destroy(slotGuns[slot].gameObject);
        slotGuns[slot] = null;
        slotGunData[slot] = w;
        if (w == null || w.prefab == null) return;
        var g = HallFx.GunDisplay(w.prefab, slotGunLength, transform);
        g.name = $"Slot{slot}";
        g.localPosition = fly && handGun != null ? transform.InverseTransformPoint(handGun.position) : SlotPos(slot, 0f);
        slotGuns[slot] = g;
        if (fly) StartCoroutine(Fly(g, slot));
    }

    IEnumerator Fly(Transform g, int slot)
    {
        Vector3 from = g.localPosition;
        float t = 0f;
        while (t < 0.28f && g != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.28f);
            float e = 1f - (1f - k) * (1f - k);
            g.localPosition = Vector3.Lerp(from, SlotPos(slot, 0f), e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
            yield return null;
        }
        if (g != null)
        {
            AbilityKit.Shockwave(g.position - Vector3.up * 0.1f, 0.6f, Theme, 0.25f);
        }
    }

    void TryEquip(int slot)
    {
        var w = Browsed;
        if (w == null) return;
        for (int i = 0; i < 4; i++)
            if (Loadout[i] == w)
            {
                if (i == slot) return;
                Flash("Already packed");
                Ui(SfxId.UiError);
                return;
            }
        int after = Spent - (Loadout[slot] != null ? Loadout[slot].orbCost : 0) + w.orbCost;
        if (after > Budget)
        {
            Flash($"Not enough orbs ({after}/{Budget})");
            Rumble.Play(Pad, 0.35f, 0.1f, 0.12f);
            Ui(SfxId.UiError);
            return;
        }
        Loadout[slot] = w;
        SetSlotGun(slot, w, fly: true);
        Play("Pick");
        Rumble.ReloadDone(Pad);
        Ui(SfxId.UiEquip);
        if (w.reloadSound != null) AudioSource.PlayClipAtPoint(w.reloadSound, transform.position, 0.6f);
        Refresh();
    }

    void Unequip(int slot)
    {
        if (Loadout[slot] == null) return;
        if (slotGuns[slot] != null) AbilityKit.Shockwave(slotGuns[slot].position, 0.5f, Dim(Theme), 0.2f);
        Loadout[slot] = null;
        SetSlotGun(slot, null, fly: false);
        Rumble.Swap(Pad);
        Ui(SfxId.UiBack);
        Refresh();
    }

    /// Drops the priciest guns until the loadout fits the current wizard's orbs
    void FitBudget()
    {
        while (Spent > Budget)
        {
            int worst = -1;
            for (int i = 0; i < 4; i++) if (Loadout[i] != null && (worst < 0 || Loadout[i].orbCost >= Loadout[worst].orbCost)) worst = i;
            if (worst < 0) break;
            Loadout[worst] = null;
            SetSlotGun(worst, null, false);
        }
    }

    void Browse(int dir)
    {
        var l = TierList;
        if (l == null || l.Length == 0) return;
        WeaponIndex = (WeaponIndex + dir + l.Length) % l.Length;
        ShowHandGun();
        Rumble.Swap(Pad);
        Ui(SfxId.UiMove);
        Refresh();
    }

    void ChangeTier(int dir)
    {
        TierIndex = (TierIndex + dir + catalog.TierCount) % catalog.TierCount;
        WeaponIndex = 0;
        ShowHandGun();
        Rumble.Swap(Pad);
        Ui(SfxId.UiMove);
        Refresh();
    }

    // ---------- input ----------

    /// Reads this player's pad (called by the director while the hall is active)
    public void Tick()
    {
        if (Pad == null || !Joined) return;
        var p = Pad;
        switch (CurrentStage)
        {
            case Stage.Wizard:
                if (p.rightShoulder.wasPressedThisFrame) ChangeWizard(+1);
                else if (p.leftShoulder.wasPressedThisFrame) ChangeWizard(-1);
                else { int s = StickFlick(p); if (s != 0) ChangeWizard(s); }
                if (p.dpad.up.wasPressedThisFrame) SetActiveRune(ActiveRune - 1);
                if (p.dpad.down.wasPressedThisFrame) SetActiveRune(ActiveRune + 1);
                if (p.dpad.right.wasPressedThisFrame) SetPassiveRune(PassiveRune + 1);
                if (p.dpad.left.wasPressedThisFrame) SetPassiveRune(PassiveRune - 1);
                if (p.buttonNorth.wasPressedThisFrame) { ShowLore = !ShowLore; Rumble.Swap(p); Ui(SfxId.UiMove); Refresh(); }
                if (p.buttonSouth.wasPressedThisFrame)
                {
                    FitBudget();
                    SetStage(Stage.Armory);
                    Play("Pick");
                    Rumble.ReloadDone(p);
                    Ui(SfxId.UiConfirm);
                }
                else if (p.buttonEast.wasPressedThisFrame) director.RequestLeave(this);
                break;

            case Stage.Armory:
                if (p.rightShoulder.wasPressedThisFrame) ChangeTier(+1);
                else if (p.leftShoulder.wasPressedThisFrame) ChangeTier(-1);
                else { int s = StickFlick(p); if (s != 0) Browse(s); }
                bool clear = p.buttonWest.isPressed;
                for (int i = 0; i < 4; i++)
                {
                    var b = i switch { 0 => p.dpad.up, 1 => p.dpad.right, 2 => p.dpad.down, _ => p.dpad.left };
                    if (!b.wasPressedThisFrame) continue;
                    if (clear) Unequip(i); else TryEquip(i);
                }
                if (p.buttonSouth.wasPressedThisFrame)
                {
                    SetStage(Stage.Ready);
                    Play("Cheer");
                    Burst(big: true);
                    Rumble.Play(p, 0.6f, 0.9f, 0.35f);
                    Ui(SfxId.UiReady);
                }
                else if (p.buttonEast.wasPressedThisFrame) { SetStage(Stage.Wizard); Play("Idle", 0.2f); Rumble.Swap(p); Ui(SfxId.UiBack); }
                break;

            case Stage.Ready:
                if (p.buttonEast.wasPressedThisFrame)
                {
                    SetStage(Stage.Armory);
                    Play("Idle", 0.2f);
                    Rumble.Swap(p);
                    Ui(SfxId.UiBack);
                }
                break;
        }
    }

    // Menu sounds pan toward this player's station
    void Ui(SfxId id) => Sfx.Play(id, transform.position);

    void SetActiveRune(int r)
    {
        ActiveRune = (r + RuneBook.ActiveCount) % RuneBook.ActiveCount;
        ApplyLook();
        Burst(big: false);
        Play("Rune");
        Rumble.Swap(Pad);
        Ui(SfxId.UiMove);
        Sfx.Play(PlayerSfx.CastSound(Elements.Of(Wizard)), transform.position, 0.35f, 1.15f);   // a taste of the spell
        Refresh();
    }

    void SetPassiveRune(int r)
    {
        PassiveRune = (r + RuneBook.PassiveCount) % RuneBook.PassiveCount;
        Burst(big: false);
        Rumble.Swap(Pad);
        Ui(SfxId.UiMove);
        Refresh();
    }

    // horizontal flick of either stick, with hold-to-repeat
    int StickFlick(Gamepad p)
    {
        float x = p.leftStick.ReadValue().x;
        if (Mathf.Abs(x) < 0.55f) x = p.rightStick.ReadValue().x;
        int dir = x > 0.55f ? 1 : x < -0.55f ? -1 : 0;
        float now = Time.unscaledTime;
        if (dir == 0) { stickDir = 0; return 0; }
        if (dir != stickDir) { stickDir = dir; nextStickAt = now + stickRepeatDelay; return dir; }
        if (now >= nextStickAt) { nextStickAt = now + stickRepeatRate; return dir; }
        return 0;
    }

    // ---------- feedback ----------

    public void Flash(string text)
    {
        flashText = text;
        flashUntil = Time.unscaledTime + 1.6f;
        Refresh();
    }

    public string FlashText => Time.unscaledTime < flashUntil ? flashText : null;

    public void Refresh() => card?.Refresh();

    static Color Dim(Color c) => Color.Lerp(c, new Color(0.3f, 0.3f, 0.4f), 0.5f);

    // ---------- per-frame visuals ----------

    void Update()
    {
        if (catalog == null) return;
        float t = Time.time;
        bool on = Joined;
        Color theme = on ? Theme : new Color(0.35f, 0.4f, 0.55f);
        Color player = PlayerColor;

        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 2.2f);
        if (flashUntil > 0f && Time.unscaledTime >= flashUntil) { flashUntil = 0f; Refresh(); }
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * (IsReady ? 4f : 1.6f) + index);
        float ringA = on ? (IsReady ? 0.75f + 0.25f * pulse : 0.45f + 0.2f * pulse) : 0.12f + 0.06f * pulse;
        GlowLine.SetColor(ring, Color.Lerp(theme, Color.white, flare * 0.6f), Mathf.Clamp01(ringA + flare));
        GlowLine.SetColor(outer, on ? player : Dim(player), on ? 0.55f : 0.18f + 0.1f * pulse);
        ring.transform.Rotate(Vector3.up, (IsReady ? 40f : 12f) * Time.deltaTime, Space.World);
        ring.widthMultiplier = 1f + flare * 0.8f;

        if (glow != null)
        {
            glow.color = theme;
            glow.intensity = on ? (IsReady ? 2.6f : 1.6f) + flare * 4f : 0f;
        }

        // ley line: a pulse travelling from the dais to the War Table
        if (ley != null)
        {
            var g = new Gradient();
            float head = Mathf.Repeat(t * (IsReady ? 0.9f : 0.45f) + index * 0.25f, 1f);
            Color lc = on ? player : new Color(0.3f, 0.35f, 0.5f);
            float baseA = on ? (IsReady ? 0.5f : 0.22f) : 0.07f;
            g.SetKeys(
                new[] { new GradientColorKey(lc, 0f), new GradientColorKey(Color.Lerp(lc, Color.white, 0.5f), head), new GradientColorKey(lc, 1f) },
                new[] { new GradientAlphaKey(baseA, 0f), new GradientAlphaKey(baseA, Mathf.Max(0f, head - 0.08f)), new GradientAlphaKey(on ? 1f : 0.15f, head), new GradientAlphaKey(baseA, Mathf.Min(1f, head + 0.08f)), new GradientAlphaKey(baseA, 1f) });
            ley.colorGradient = g;
        }

        // sockets show in the armory (where the d-pad slots go)
        for (int i = 0; i < 4; i++)
        {
            bool show = CurrentStage == Stage.Armory;
            sockets[i].enabled = show;
            if (show)
            {
                bool filled = Loadout[i] != null;
                GlowLine.SetColor(sockets[i], filled ? theme : player, filled ? 0.7f : 0.25f + 0.2f * pulse);
                sockets[i].transform.localPosition = SlotPos(i, 0f) - Vector3.up * 0.35f;
            }
        }

        // guns: fixed at their d-pad side while packing, orbiting once ready
        if (IsReady) orbit += Time.deltaTime * 55f;
        else orbit = Mathf.MoveTowards(orbit, Mathf.Round(orbit / 360f) * 360f, Time.deltaTime * 400f);
        for (int i = 0; i < 4; i++)
        {
            var g = slotGuns[i];
            if (g == null) continue;
            float o = Mathf.Repeat(orbit, 360f);
            Vector3 target = o > 0.01f && o < 359.99f ? SlotPos(i, o) : SlotPos(i, 0f);
            target += Vector3.up * Mathf.Sin(t * 1.6f + i * 1.3f) * 0.07f;
            g.localPosition = Vector3.Lerp(g.localPosition, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            // side-on to the camera, nose following the orbit
            Vector3 tangent = Vector3.Cross(Vector3.up, (g.position - transform.position).normalized);
            Vector3 nose = IsReady ? -tangent : (i == 3 ? -HallCamera.GroundRight : HallCamera.GroundRight);
            g.rotation = Quaternion.Slerp(g.rotation, Quaternion.LookRotation(nose) * Quaternion.Euler(0f, -90f, 0f), 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        PlaceHandGun(1f);

        // banner unfurls while the player is in, rolled up otherwise
        if (banner != null)
        {
            var sc = banner.localScale;
            sc.y = Mathf.Lerp(sc.y, on ? 1f : 0.12f, 1f - Mathf.Exp(-5f * Time.deltaTime));
            banner.localScale = sc;
        }

        // the wizard turns a little toward their hand gun while shopping
        if (model != null)
        {
            float yaw = CurrentStage == Stage.Armory ? -32f : CurrentStage == Stage.Ready ? 0f : -18f;
            var want = HallCamera.FacingCamera * Quaternion.Euler(0f, yaw, 0f);
            model.transform.rotation = Quaternion.Slerp(model.transform.rotation, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        // a pad that unplugs leaves its spot
        if (Joined && (Pad == null || !Pad.added)) director.RequestLeave(this);
    }

    // ---------- results ----------

    /// The loadout as the game expects it: 4 slots, empty ones hold the placeholder
    public WeaponData[] GameLoadout()
    {
        var a = new WeaponData[4];
        for (int i = 0; i < 4; i++) a[i] = Loadout[i] != null ? Loadout[i] : catalog.placeholder;
        return a;
    }

    public Vector3 HeadPosition => transform.position + Vector3.up * 3.2f;
}
