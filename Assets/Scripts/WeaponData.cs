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

    // Additional properties can be added as needed
}