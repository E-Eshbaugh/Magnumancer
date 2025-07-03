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
    public float recoil;
    public GameObject ammoType;
    [Header("Sniper Abilitites")]
    public float laserRange;
    [Header("Shotgun Abilities")]
    public bool isShotgun;
    public int pelletCount = 5;
    public float spreadAngle = 15f;
    [Header("Bullet Abilities")]
    public GameObject specialBulletType;
    public GameObject baseAmmoType;

    // Additional properties can be added as needed
}