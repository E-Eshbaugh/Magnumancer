using UnityEngine;

/// <summary>
/// Elemental Rounds: a fresh magazine loaded with a random element that isn't yours.
/// Every round looks like that element and leaves its status (fire brands, frost chills,
/// water soaks, lightning charges, earth staggers, poison poisons), so anyone can set up
/// reactions they normally can't. Lasts until the magazine is spent, you reload, swap
/// guns or lose a life.
/// </summary>
public class ElementalRounds : MonoBehaviour
{
    /// Elements whose statuses bullets can build (nature roots and void marks stay ability-only)
    public static readonly Element[] Pool =
        { Element.Fire, Element.Frost, Element.Water, Element.Lightning, Element.Earth, Element.Poison };

    public Element Element { get; private set; }

    AmmoControl ammo;
    int gunIndex, capacity, roundsLeft;
    BuffRing ring;
    Color color;
    float nextMote;
    System.Func<Element> shotElement;

    /// Loads the picker's held gun; returns the element (None if they have no gun)
    public static Element Give(GameObject player)
    {
        var ammo = player.GetComponentInChildren<AmmoControl>();
        if (ammo == null || ammo.currentGun == null) return Element.None;

        var r = player.GetComponent<ElementalRounds>();
        if (r == null) r = player.AddComponent<ElementalRounds>();

        Element own = Elements.Of(player);
        Element pick;
        int guard = 0;
        do pick = Pool[Random.Range(0, Pool.Length)];
        while ((pick == own || pick == r.Element) && ++guard < 20);

        r.Begin(ammo, pick);
        return pick;
    }

    void Begin(AmmoControl gun, Element element)
    {
        Unhook();
        ammo = gun;
        Element = element;
        Sfx.Play(PlayerSfx.CastSound(element), transform.position, 0.6f, 1.2f);
        color = Elements.ColorOf(element);

        ammo.RefillMagazine();
        capacity = Mathf.Max(1, ammo.currentGun.ammoCapacity);
        roundsLeft = capacity;
        gunIndex = ammo.currentGunIndex;

        shotElement = () => Element;
        ammo.ShotElement = shotElement;
        ammo.OnFired += Fired;
        ammo.OnReloaded += Reloaded;
        DamageEvents.Killed += OnKilled;

        if (ring == null) ring = new BuffRing(transform, 1f, color);
        ring.SetColor(color);
        AbilityKit.Shockwave(transform.position, 2f, color, 0.3f);
    }

    void Fired(int damage)
    {
        if (--roundsLeft <= 0) Destroy(this);
    }

    void Reloaded() => Destroy(this);

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == gameObject) Destroy(this);
    }

    void Update()
    {
        if (ammo == null || ammo.currentGunIndex != gunIndex) { Destroy(this); return; }
        ring.Draw(transform.position, roundsLeft / (float)capacity, 0.6f);

        // element bits drift off the loaded gun
        if (Time.time >= nextMote)
        {
            nextMote = Time.time + 0.15f;
            var fire = ammo.GetComponent<FireController3D>();
            if (fire != null && fire.firePoint != null)
                BulletFX.Mote(Elements.FlavorOf(Element), color, fire.firePoint.position, 0.7f);
        }
    }

    void Unhook()
    {
        DamageEvents.Killed -= OnKilled;
        if (ammo == null) return;
        ammo.OnFired -= Fired;
        ammo.OnReloaded -= Reloaded;
        if (ammo.ShotElement == shotElement) ammo.ShotElement = null;
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) Destroy(this); }   // eliminated: the player switches off

    void OnDestroy()
    {
        Unhook();
        ring?.Destroy();
    }
}
