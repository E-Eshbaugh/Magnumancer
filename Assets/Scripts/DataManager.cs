using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // the array you want to carry forward
    public WeaponData[] p1_loadout;
    public WeaponData[] p2_loadout;
    public WeaponData[] p3_loadout;
    public WeaponData[] p4_loadout;
    public WizardData[] selectedWizards = new WizardData[4];
    public int numPlayers;
    public int selectedMode = 0; // 0 = DM, 1 = TDM, 2 = Waves

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetWizard(int slot, WizardData data)
    {
        if (slot < 0 || slot >= selectedWizards.Length) return;
        selectedWizards[slot] = data;
    }

    public WizardData GetWizard(int slot)
    {
        return (slot >= 0 && slot < selectedWizards.Length) ? selectedWizards[slot] : null;
    }
}
