using UnityEngine;

[CreateAssetMenu(menuName = "Mangumancer/Wizard")]
public class WizardData : ScriptableObject
{
    public string wizardName;
    public int heartCount;
    public int orbCount;
    public int loadoutOrbs;
    public Sprite factionEmblem;
    public Sprite charcterImage;
    public string passiveAbility;
    public string activeAbility;
    public string loreText;
}