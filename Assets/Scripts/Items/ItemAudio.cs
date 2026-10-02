using UnityEngine;

/// <summary>
/// Item drop sounds. Clips are slots on the sound bank (Resources/ReactionSounds, "Item
/// drops" section). Until a slot is filled, the game's own effects from Resources/SfxBank
/// play instead (the strike also borrows the Conduct crackle as its thunder).
/// </summary>
public static class ItemAudio
{
    static ReactionSoundBank Bank => ReactionAudio.Sounds;
    static float Volume => Bank != null ? Bank.itemVolume : 0.8f;

    public static void Incoming(bool rare)
    {
        var b = Bank;
        if (b != null && b.dropIncoming != null) ReactionAudio.PlayClip(b.dropIncoming, Volume, rare ? 0.85f : 1f);
        else Sfx.Play(SfxId.DropIncoming, 1f, rare ? 0.85f : 1f);
        if (rare && b != null && b.rareDropAnnouncer != null) ReactionAudio.PlayClip(b.rareDropAnnouncer, b.announcerVolume);
    }

    public static void Land(bool rare)
    {
        var b = Bank;
        if (b != null && b.dropLand != null) { ReactionAudio.PlayClip(b.dropLand, Volume, rare ? 0.85f : 1f); return; }
        Sfx.Play(SfxId.DropLand, rare ? 1f : 0.8f, rare ? 0.8f : 1f);
        var thunder = b != null ? b.Find(Reaction.Conduct) : null;
        if (thunder != null && thunder.clips != null && thunder.clips.Length > 0)
            ReactionAudio.PlayClip(thunder.clips[Random.Range(0, thunder.clips.Length)], thunder.volume * 0.7f,
                                   rare ? 0.7f : 0.85f, 0.9f);
    }

    public static void Pickup(Rarity rarity)
    {
        var b = Bank;
        if (b != null && b.pickup != null)
            ReactionAudio.PlayClip(b.pickup, Volume, rarity >= Rarity.Rare ? 0.85f : Random.Range(0.98f, 1.06f));
        else Sfx.Play(rarity >= Rarity.Rare ? SfxId.PickupRare : SfxId.Pickup);
    }

    public static void CrownTaken()
    {
        var b = Bank;
        if (b != null && b.crownTaken != null) ReactionAudio.PlayClip(b.crownTaken, Volume);
        else Sfx.Play(SfxId.CrownTaken);
    }
}
