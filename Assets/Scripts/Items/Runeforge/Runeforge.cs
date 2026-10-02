using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// The Zombies Pack-a-Punch (pack-a-punch.md): a stone-and-brass chest that wakes once the
/// run reaches its late waves. Stand next to it holding a loadout gun and hold X to forge
/// that gun with your points (ForgedRunes decides what the forge does for your build).
///
/// Every Zombies map gets one automatically. Without an authored one in the scene it rises
/// out of the floor beside the party when it unlocks; an authored Runeforge stays put and
/// shows closed until then. Unlock is shared, purchases are per player and per gun.
/// </summary>
public class Runeforge : MonoBehaviour
{
    [Tooltip("Wakes once this wave has been cleared (0 = open from the start, for testing). " +
             "Placeholder until maps have a final-stage signal; call Unlock() from one instead.")]
    [Min(0)] public int unlockAfterWave = 5;
    [Min(1)] public int price = ForgedRunes.Price;
    [Min(0.5f)] public float range = 2.5f;
    [Min(0.1f)] public float holdTime = 0.75f;

    static readonly Color Gold = new Color(1f, 0.78f, 0.3f);
    const string BlockKey = "runeforge";

    bool authored = true, unlocked, built;
    GoblinSpawner spawner;
    Transform lidLeft, lidRight, relic;
    LineRenderer rangeRing;
    readonly List<Renderer> grooves = new();
    readonly Dictionary<PlayerHealthControl, Buyer> buyers = new();

    class Buyer
    {
        public AmmoControl ammo;
        public int slot = -1;
        public float progress;
        public bool needRelease;
        public TextMeshPro prompt;
        public LineRenderer ring;
    }

    // ---------- Setup ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (FindAnyObjectByType<GoblinSpawner>() == null || FindAnyObjectByType<Runeforge>() != null) return;
        var forge = new GameObject("Runeforge").AddComponent<Runeforge>();
        forge.authored = false;   // placed beside the party when it wakes
    }

    /// Opens every forge now (for a future final-stage trigger)
    public static void UnlockAll()
    {
        foreach (var f in FindObjectsByType<Runeforge>(FindObjectsSortMode.None)) f.Unlock();
    }

    void Start()
    {
        spawner = FindAnyObjectByType<GoblinSpawner>();
        if (authored) Build(transform.position);
    }

    void OnDisable()
    {
        foreach (var b in buyers.Values)
        {
            if (b.ammo != null) b.ammo.SetReloadButtonBlocked(BlockKey, false);
            if (b.prompt != null) Destroy(b.prompt.gameObject);
            if (b.ring != null) Destroy(b.ring.gameObject);
        }
        buyers.Clear();
    }

    bool ShouldUnlock()
    {
        if (unlockAfterWave <= 0) return true;
        if (spawner == null) return false;
        int wave = spawner.CurrentWave;
        return wave > unlockAfterWave
            || wave == unlockAfterWave && !spawner.WaveActive && spawner.PendingCount == 0 && spawner.AliveCount == 0;
    }

    public void Unlock()
    {
        if (unlocked) return;
        unlocked = true;
        StartCoroutine(Awaken());
    }

    void Update()
    {
        if (!unlocked)
        {
            if (ShouldUnlock()) Unlock();
            return;
        }
        if (!built) return;

        if (relic != null)
        {
            relic.localPosition = new Vector3(0f, 1.55f + 0.08f * Mathf.Sin(Time.time * 2f), 0f);
            relic.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }
        if (rangeRing != null) GlowLine.SetColor(rangeRing, Gold, 0.25f + 0.1f * Mathf.Sin(Time.time * 3f));

        foreach (var p in PlayerHealthControl.ActivePlayers)
            if (p != null) Serve(p);
    }

    // ---------- Buying ----------

    void Serve(PlayerHealthControl player)
    {
        if (!buyers.TryGetValue(player, out var b))
            buyers[player] = b = new Buyer { ammo = player.GetComponentInChildren<AmmoControl>() };
        var ammo = b.ammo;
        if (ammo == null) return;

        Vector3 to = player.transform.position - transform.position;
        bool inRange = player.IsStanding && Mathf.Abs(to.y) < 2f && new Vector2(to.x, to.z).magnitude <= range;

        string text = null;
        bool canBuy = false;
        if (inRange)
        {
            var wizard = ForgedRunes.WizardOf(player.gameObject);
            int rune = ForgedRunes.ActiveRuneOf(player.gameObject);
            if (ammo.currentGun == null || ammo.FiringBlocked) text = "Equip a loadout weapon";
            else if (ammo.CurrentForged) text = $"{ForgedRunes.ForgedName(ammo.currentGun, wizard, rune)}\n<size=70%>Already forged</size>";
            else
            {
                canBuy = ZombiesPoints.Get(player.gameObject) >= price;
                var up = ForgedRunes.UpgradeFor(wizard, rune);
                string effect = up != null ? up.summary : "Hits harder, holds more rounds.";
                string cost = canBuy ? $"Hold X  ·  {price:N0}" : $"<color=#FF9988>Need {price:N0}</color>";
                text = $"{ForgedRunes.ForgedName(ammo.currentGun, wizard, rune)}\n<size=60%>{effect}</size>\n<size=80%>{cost}</size>";
            }
        }

        ammo.SetReloadButtonBlocked(BlockKey, canBuy);
        ShowPrompt(b, player, text);

        // Hold X to buy. Leaving range, going down or releasing cancels for free; swapping
        // guns mid-hold (or finishing a purchase) needs a fresh press.
        var pad = ammo.gamepad;
        bool pressed = pad != null && pad.buttonWest.isPressed && !GamePause.InputBlocked;
        if (!pressed) b.needRelease = false;
        if (b.slot != ammo.currentGunIndex)
        {
            if (b.progress > 0f) b.needRelease = true;
            b.progress = 0f;
            b.slot = ammo.currentGunIndex;
        }
        if (canBuy && pressed && !b.needRelease)
        {
            b.progress += Time.deltaTime;
            if (b.progress >= holdTime)
            {
                b.progress = 0f;
                b.needRelease = true;
                Purchase(player, ammo);
            }
        }
        else b.progress = 0f;
        DrawProgress(b, player, b.progress / holdTime);
    }

    void Purchase(PlayerHealthControl player, AmmoControl ammo)
    {
        // validate everything before taking the points, then forge in the same step
        int slot = ammo.currentGunIndex;
        var gun = ammo.currentGun;
        if (gun == null || ammo.IsForged(slot) || ammo.FiringBlocked || !player.IsStanding) return;
        var wizard = ForgedRunes.WizardOf(player.gameObject);
        int rune = ForgedRunes.ActiveRuneOf(player.gameObject);
        string forgedName = ForgedRunes.ForgedName(gun, wizard, rune);
        if (!ZombiesPoints.TrySpend(player.gameObject, price)) return;
        ammo.ForgeSlot(slot, ForgedRunes.DamageMultiplier, ForgedRunes.MagazineMultiplier);

        Color shade = AbilityKit.Theme(player.gameObject);
        Vector3 top = transform.position + Vector3.up * 1.5f;
        PowerFx.Flash(top, Gold, 9f, 6f, 0.4f);
        PowerFx.Sparks(top, Gold, 30, 6f, 0.6f, 0.08f, 1f);
        PowerFx.Sparks(AbilityKit.Chest(player.gameObject), shade, 20, 4f, 0.5f, 0.07f, -0.5f);
        AbilityKit.Zap(top, AbilityKit.Chest(player.gameObject), shade, 0.35f, 0.25f);
        AbilityKit.Shockwave(transform.position, range, Gold, 0.5f);
        CameraShake.Shake(0.15f, 0.2f);
        Rumble.Play(player.gameObject, 0.7f, 0.9f, 0.35f);
        ItemGetText.Show(forgedName.ToUpperInvariant(), Gold, shade, player.gameObject, 0.8f);
    }

    // ---------- Prompt and hold ring ----------

    void ShowPrompt(Buyer b, PlayerHealthControl player, string text)
    {
        if (text == null)
        {
            if (b.prompt != null) b.prompt.gameObject.SetActive(false);
            return;
        }
        if (b.prompt == null)
        {
            if (TMP_Settings.defaultFontAsset == null) return;
            var go = new GameObject("RuneforgePrompt");
            b.prompt = go.AddComponent<TextMeshPro>();
            b.prompt.font = TMP_Settings.defaultFontAsset;
            b.prompt.fontStyle = FontStyles.Bold;
            b.prompt.alignment = TextAlignmentOptions.Center;
            b.prompt.textWrappingMode = TextWrappingModes.Normal;
            b.prompt.outlineWidth = 0.22f;
            b.prompt.outlineColor = new Color32(16, 12, 24, 255);
            b.prompt.fontSize = 4.5f;
            b.prompt.sortingOrder = 57;
            b.prompt.color = Gold;
            b.prompt.rectTransform.sizeDelta = new Vector2(9f, 4f);
        }
        b.prompt.gameObject.SetActive(true);
        b.prompt.text = text;
        b.prompt.transform.position = player.transform.position + Vector3.up * 3.6f;
        var cam = Camera.main;
        if (cam != null) b.prompt.transform.rotation = cam.transform.rotation;
    }

    void DrawProgress(Buyer b, PlayerHealthControl player, float k)
    {
        if (k <= 0f)
        {
            if (b.ring != null) b.ring.enabled = false;
            return;
        }
        if (b.ring == null)
        {
            var go = new GameObject("RuneforgeHold");
            b.ring = GlowLine.Make(go.transform, "Arc", 33, 0.1f, AbilityKit.Glow());
        }
        b.ring.enabled = true;
        Vector3 c = player.transform.position + Vector3.up * 0.08f;
        int n = b.ring.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.PI * 0.5f - i / (float)(n - 1) * Mathf.PI * 2f * Mathf.Clamp01(k);
            b.ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.9f);
        }
        GlowLine.SetColor(b.ring, Gold, 1f);
    }

    // ---------- Placement and art ----------

    IEnumerator Awaken()
    {
        if (!authored) transform.position = PickSpot();
        Build(transform.position);
        built = false;   // no buying until the lid is open

        // rise out of the floor (auto-placed) and crack the lid
        var body = transform.GetChild(0);
        Vector3 rest = body.localPosition;
        if (!authored)
        {
            RockDebris.Dust(transform.position, 1.6f, 14);
            CameraShake.Shake(0.2f, 0.6f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                body.localPosition = rest + Vector3.down * 1.2f * (1f - Mathf.SmoothStep(0f, 1f, t / 0.6f));
                if (Random.value < 0.3f) RockDebris.Chunk(transform.position + Random.insideUnitSphere * 0.8f, Random.onUnitSphere * 2f + Vector3.up * 4f, 0.15f, 0.7f);
                yield return null;
            }
            body.localPosition = rest;
        }

        Vector3 l0 = lidLeft.localPosition, r0 = lidRight.localPosition;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            lidLeft.localPosition = l0 + new Vector3(-0.45f * k, 0.1f * k, 0f);
            lidRight.localPosition = r0 + new Vector3(0.45f * k, 0.1f * k, 0f);
            lidLeft.localRotation = Quaternion.Euler(0f, 0f, 35f * k);
            lidRight.localRotation = Quaternion.Euler(0f, 0f, -35f * k);
            yield return null;
        }

        Element[] elements = { Element.Fire, Element.Frost, Element.Water, Element.Lightning, Element.Earth, Element.Nature, Element.Poison, Element.Void };
        for (int i = 0; i < grooves.Count; i++) Tint(grooves[i], Elements.ColorOf(elements[i % elements.Length]), 2f);

        relic = AbilityKit.GlowOrb(Gold, 0.35f).transform;
        relic.SetParent(transform, false);
        var light = relic.gameObject.AddComponent<Light>();
        light.color = Gold; light.range = 5f; light.intensity = 3f;

        rangeRing = GlowLine.Make(transform, "Range", 48, 0.06f, AbilityKit.Glow());
        rangeRing.loop = true;
        AbilityKit.Circle(rangeRing, transform.position + Vector3.up * 0.06f, range);

        Vector3 top = transform.position + Vector3.up * 1.5f;
        PowerFx.Flash(top, Gold, 10f, 10f, 0.6f);
        PowerFx.Sparks(top, Gold, 40, 7f, 0.8f, 0.08f, 0.5f);
        AbilityKit.Shockwave(transform.position, 4f, Gold, 0.6f);
        ItemGetText.Show("THE RUNEFORGE AWAKENS", Gold, new Color(1f, 0.45f, 0.15f), gameObject, 1.1f);
        built = true;
    }

    /// Open floor beside the party: on the navmesh, clear of walls, not on anyone's head
    Vector3 PickSpot()
    {
        Vector3 centre = Vector3.zero;
        int count = 0;
        foreach (var p in PlayerHealthControl.ActivePlayers)
            if (p != null && p.IsStanding) { centre += p.transform.position; count++; }
        if (count > 0) centre /= count;
        else if (spawner != null) centre = spawner.center;

        for (float r = 3.5f; r <= 7f; r += 1.5f)
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Vector3 c = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!NavMesh.SamplePosition(c, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (Physics.CheckBox(p + Vector3.up * 0.7f, new Vector3(0.95f, 0.55f, 0.75f), Quaternion.identity,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
        return NavMesh.SamplePosition(centre, out var fallback, 6f, NavMesh.AllAreas) ? fallback.position : centre;
    }

    void Build(Vector3 at)
    {
        if (transform.childCount > 0) return;
        transform.position = at;
        var body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        var stone = RockShapes.DarkStone();
        var brass = RockDebris.StoneMaterial(new Color(0.78f, 0.56f, 0.22f));

        var chest = Block(body, "Chest", new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 0.8f, 1f), stone);
        var obstacle = chest.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        Block(body, "Plinth", new Vector3(0f, 0.05f, 0f), new Vector3(1.75f, 0.1f, 1.2f), stone);
        lidLeft = Block(body, "LidL", new Vector3(-0.375f, 0.89f, 0f), new Vector3(0.75f, 0.18f, 1.05f), brass);
        lidRight = Block(body, "LidR", new Vector3(0.375f, 0.89f, 0f), new Vector3(0.75f, 0.18f, 1.05f), brass);
        Destroy(lidLeft.GetComponent<Collider>());
        Destroy(lidRight.GetComponent<Collider>());

        // eight elemental grooves around the chest, dark until it wakes
        for (int i = 0; i < 8; i++)
        {
            bool front = i < 4;
            float x = -0.54f + (i % 4) * 0.36f;
            var g = AbilityKit.GlowOrb(Color.gray, 0.14f);
            g.transform.SetParent(body, false);
            g.transform.localPosition = new Vector3(x, 0.45f, front ? -0.51f : 0.51f);
            g.transform.localScale = new Vector3(0.12f, 0.4f, 0.05f);
            var r = g.GetComponent<Renderer>();
            Tint(r, new Color(0.3f, 0.28f, 0.25f), 0.3f);
            grooves.Add(r);
        }
    }

    static Transform Block(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    static void Tint(Renderer r, Color c, float intensity)
    {
        var block = new MaterialPropertyBlock();
        var col = c * intensity; col.a = 1f;
        block.SetColor("_BaseColor", col);
        block.SetColor("_Color", col);
        r.SetPropertyBlock(block);
    }
}
