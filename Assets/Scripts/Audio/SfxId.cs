/// <summary>
/// Every game sound effect (see Sfx / SfxBank). Values are explicit because the bank asset
/// stores them as numbers: add new ones with a new number, never renumber.
/// </summary>
public enum SfxId
{
    None = 0,

    // ---------- Movement ----------
    Footstep = 10,
    Jump = 11,
    DoubleJump = 12,
    Land = 13,
    LandHeavy = 14,
    Dash = 15,
    Blink = 16,

    // ---------- Guns (family-specific clips live in SfxBank.gunFamilies) ----------
    BulletImpact = 30,
    Ricochet = 31,
    MagEmpty = 32,
    GunAbility = 33,
    ScopeFocus = 34,
    Overclock = 35,
    LauncherThunk = 36,
    DetonatorClick = 37,

    // ---------- Combat ----------
    HitMarker = 50,
    Hurt = 51,
    KillConfirm = 52,
    LifeLost = 53,
    Eliminated = 54,
    Respawn = 55,
    Heal = 56,
    Heartbeat = 57,
    MonsterHit = 58,
    MonsterDeath = 59,
    Explosion = 60,
    ExplosionBig = 61,
    Quake = 62,
    PropHit = 63,
    PropBreak = 64,
    Stun = 65,

    // ---------- Wizard abilities ----------
    CastFire = 80,
    CastFrost = 81,
    CastWater = 82,
    CastLightning = 83,
    CastEarth = 84,
    CastNature = 85,
    CastPoison = 86,
    CastVoid = 87,
    CastArcane = 88,
    AbilityReady = 89,
    Zap = 90,
    Splash = 91,
    Gust = 92,
    GasBurst = 93,

    // ---------- Items ----------
    DropIncoming = 110,
    DropLand = 111,
    Pickup = 112,
    PickupRare = 113,
    CrownTaken = 114,
    Throw = 115,
    ShieldUp = 116,
    ShieldBlock = 117,
    ShieldBreak = 118,
    Points = 119,
    Purchase = 120,
    PowerUp = 121,
    StickyThunk = 122,
    FuseHiss = 123,

    // ---------- Wonder weapons ----------
    EmberSpinLoop = 140,
    EmberShot = 141,
    FrostCannon = 142,
    GaleHorn = 143,
    SingularityShot = 144,
    ThunderMaul = 146,

    // ---------- Match flow & announcer ----------
    MatchStart = 160,
    Victory = 161,
    Defeat = 162,
    WaveStart = 163,
    KillingSpree = 164,
    Headshot = 165,

    // ---------- UI ----------
    UiMove = 180,
    UiConfirm = 181,
    UiBack = 182,
    UiReady = 183,
    UiError = 184,
    UiPause = 185,
    UiUnpause = 186,
    UiLaunch = 188,
    UiEquip = 189,
}

/// Per-weapon-family handling sounds (draw, dry fire, reload finish, shell/slug load)
public enum GunSfx { Draw, DryFire, ReloadDone, SlugLoad }
