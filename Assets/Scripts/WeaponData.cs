using UnityEngine;

[CreateAssetMenu(menuName = "Magnumancer/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int damage;
    public int ammoCapacity;
    public int weight;
    public float attackSpeed;
    public Sprite weaponIcon;
    public string description;

    // Additional properties can be added as needed
}