using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anti-snowball: whoever's leading (most kills, then most lives left) wears a glowing
/// gold crown everyone can see. Taking a life from the crown holder pays out (ability
/// ready, magazine refilled) and the crown moves to whoever leads now (ties go to the
/// one who took it). Nobody wears it while everyone's even. Runs alongside DropDirector.
/// </summary>
public class LeaderCrown : MonoBehaviour
{
    public float height = 2.7f;
    public float radius = 0.38f;

    readonly Dictionary<GameObject, int> kills = new();
    GameObject holder;
    Transform crown;
    LineRenderer band, points;
    Light glow;
    float bornAt;

    public GameObject Holder => holder;

    void OnEnable() => DamageEvents.Killed += OnKilled;
    void OnDisable() => DamageEvents.Killed -= OnKilled;

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == null || victim.GetComponent<PlayerHealthControl>() == null) return;
        bool byPlayer = attacker != null && attacker != victim && attacker.GetComponent<PlayerHealthControl>() != null;
        if (byPlayer)
        {
            kills.TryGetValue(attacker, out int k);
            kills[attacker] = k + 1;
        }

        GameObject favored = null;
        if (victim == holder && byPlayer)
        {
            CrownBreaker(attacker, victim);
            favored = attacker;
        }
        // the life count changes after this event: decide on the next frame
        pendingFavored = favored;
        recheckAt = Time.frameCount + 1;
    }

    GameObject pendingFavored;
    int recheckAt = -1;

    void CrownBreaker(GameObject killer, GameObject victim)
    {
        var ability = killer.GetComponentInChildren<WizardAbilityController>();
        if (ability != null && ability.Cooldown != null) ability.Cooldown.Refresh();
        foreach (var ammo in killer.GetComponentsInChildren<AmmoControl>()) ammo.RefillMagazine();

        Vector3 at = crown != null ? crown.position : AbilityKit.Chest(victim) + Vector3.up * 1.5f;
        PowerFx.Sparks(at, ItemBook.Gold, 40, 7f, 0.7f, 0.08f, 1f);
        PowerFx.Flash(at, ItemBook.Gold, 8f, 6f, 0.4f);
        ReactionPopup.Show("CROWN BREAKER!", ItemBook.Gold, Color.white, AbilityKit.Chest(killer) + Vector3.up * 2f, 1f);
        Rumble.Play(killer, 0.6f, 1f, 0.35f);
        ItemAudio.CrownTaken();
    }

    void Update()
    {
        if (recheckAt >= 0 && Time.frameCount >= recheckAt)
        {
            recheckAt = -1;
            Decide(pendingFavored);
            pendingFavored = null;
        }
        // the holder was knocked out of the match
        if (holder != null && (!holder.activeInHierarchy || Health(holder) == null || !Health(holder).IsStanding))
            Decide(null);

        Draw();
    }

    void Decide(GameObject favored)
    {
        var players = DropDirector.PlayersInMatch();
        PlayerHealthControl best = null;
        bool tie = false;
        foreach (var p in players)
        {
            if (best == null) { best = p; continue; }
            int c = Compare(p, best);
            if (c > 0) { best = p; tie = false; }
            else if (c == 0) tie = true;
        }

        GameObject leader = best != null && !tie ? best.gameObject : null;
        if (tie && best != null)
        {
            // among the tied leaders, it goes to the one who just took it, or stays put
            bool favoredTied = false, holderTied = false;
            foreach (var p in players)
            {
                if (Compare(p, best) != 0) continue;
                if (favored != null && p.gameObject == favored) favoredTied = true;
                if (holder != null && p.gameObject == holder) holderTied = true;
            }
            leader = favoredTied ? favored : holderTied ? holder : null;
        }
        // nobody's ahead of anybody yet
        if (leader != null && AllEven()) leader = null;
        SetHolder(leader);
    }

    int Compare(PlayerHealthControl a, PlayerHealthControl b)
    {
        kills.TryGetValue(a.gameObject, out int ka);
        kills.TryGetValue(b.gameObject, out int kb);
        if (ka != kb) return ka.CompareTo(kb);
        return a.LivesLeft.CompareTo(b.LivesLeft);
    }

    bool AllEven()
    {
        PlayerHealthControl first = null;
        foreach (var p in DropDirector.PlayersInMatch())
        {
            if (first == null) { first = p; continue; }
            if (Compare(p, first) != 0) return false;
        }
        return true;
    }

    void SetHolder(GameObject next)
    {
        if (next == holder) return;
        holder = next;
        if (holder == null) { if (crown != null) crown.gameObject.SetActive(false); return; }
        if (crown == null) Build();
        crown.gameObject.SetActive(true);
        bornAt = Time.time;
        Vector3 at = holder.transform.position + Vector3.up * height;
        PowerFx.Sparks(at, ItemBook.Gold, 20, 4f, 0.5f, 0.06f, 0.5f);
        PowerFx.Flash(at, ItemBook.Gold, 4f, 4f, 0.3f);
    }

    static PlayerHealthControl Health(GameObject go) => go != null ? go.GetComponent<PlayerHealthControl>() : null;

    // ---------- visuals ----------

    void Build()
    {
        crown = new GameObject("LeaderCrown").transform;
        var mat = AbilityKit.Glow();
        band = GlowLine.Make(crown, "Band", 33, 0.07f, mat);
        band.useWorldSpace = false;
        band.loop = true;
        points = GlowLine.Make(crown, "Points", 41, 0.06f, mat);
        points.useWorldSpace = false;
        points.loop = true;
        for (int i = 0; i < 33; i++)
        {
            float a = i / 33f * Mathf.PI * 2f;
            band.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
        // five points: the rim zig-zags up to a tip and back down
        for (int i = 0; i < 41; i++)
        {
            float f = i / 41f;
            float a = f * Mathf.PI * 2f;
            float tooth = 1f - Mathf.Abs(Mathf.Repeat(f * 5f, 1f) * 2f - 1f);   // 0 at the dips, 1 at the tips
            points.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.05f + tooth * 0.32f, Mathf.Sin(a) * radius));
        }
        GlowLine.SetColor(band, ItemBook.Gold, 1f);
        GlowLine.SetColor(points, Color.Lerp(ItemBook.Gold, Color.white, 0.3f), 1f);

        var jewel = AbilityKit.GlowOrb(new Color(1f, 0.25f, 0.3f), 0.1f);
        jewel.transform.SetParent(crown, false);
        jewel.transform.localPosition = new Vector3(0f, 0.12f, -radius);

        glow = crown.gameObject.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = ItemBook.Gold; glow.range = 2.5f; glow.intensity = 1.5f; glow.shadows = LightShadows.None;
    }

    void Draw()
    {
        if (crown == null || holder == null) return;
        bool show = !WizardSpawnEffect.IsArriving(holder.transform);
        crown.gameObject.SetActive(show);
        if (!show) return;
        float age = Time.time - bornAt;
        float pop = age < 0.3f ? Mathf.Lerp(0.3f, 1.2f, age / 0.3f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((age - 0.3f) / 0.15f));
        crown.position = holder.transform.position + Vector3.up * (height + 0.06f * Mathf.Sin(Time.time * 2.5f));
        crown.rotation = Quaternion.Euler(8f, Time.time * 50f, 0f);
        crown.localScale = Vector3.one * pop;
        glow.intensity = 1.2f + 0.4f * Mathf.Sin(Time.time * 4f);
    }

    void OnDestroy()
    {
        if (crown != null) Destroy(crown.gameObject);
    }
}
