using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Spells/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int damage;
    public int ammoCapacity;
    public float attackSpeed;
    public int weight;
    public int orbCost;
    public Sprite weaponIcon;
    public string description;
    public string fireType;
    [Tooltip("Weapon family, used for wizard weapon synergies")]
    public WeaponClass weaponClass;
    public float recoil;
    public float reloadTime;
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public GameObject ammoType;
    [Header("Sniper Abilitites")]
    public float laserRange;
    [Header("Shotgun Abilities")]
    public bool isShotgun;
    public int pelletCount = 5;
    public float spreadAngle = 15f;
    [Header("Heavy Weapon Abilities")]
    public bool heavyWeapon;
    [Header("Smg Abilities")]
    public bool akimbo;

    [Header("AR Abilities")]
    public bool grenadeLauncher;
    [Header("Explosion/Special")]
    public bool megaBomb;
    [Tooltip("LT: Remote Fuse — hold to keep your grenades from going off, release to detonate them all")]
    public bool remoteDetonate;

    [Header("Bullet Abilities")]
    public GameObject specialBulletType;
    public GameObject baseAmmoType;
    public GameObject prefab;

    // Additional properties can be added as needed
}

public enum WeaponClass
{
    None = 0,
    Sniper = 1,
    Rifle = 2,
    SMG = 3,
    Shotgun = 4,
    Heavy = 5,
    Launcher = 6
}
