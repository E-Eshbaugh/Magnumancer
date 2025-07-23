using UnityEngine;
using UnityEngine.UI;

public class MagicManagement : MonoBehaviour
{
    [Header("-- UI --")]
    public Image elementalEmblem;
    public GameObject[] Orbs;

    [Header("-- Runtime --")]
    [Tooltip("Which player this UI belongs to (0-based). Set by MenuNavigationControl/WeaponSelectControl.")]
    public int playerIndex = 0;

    public int spentOrbs = 0;   // WeaponSelectControl updates this through OnSpentChanged()

    // cached
    private WizardData cachedWizard;
    private int totalOrbs;

    void OnEnable()
    {
        CacheWizard();
        RefreshUI();
    }

    /// <summary>Call when switching which player is editing loadout.</summary>
    public void SetPlayer(int index)
    {
        playerIndex = index;
        CacheWizard();
        RefreshUI();
    }

    /// <summary>Call whenever inventory changes.</summary>
    public void OnSpentChanged(int newSpent)
    {
        spentOrbs = Mathf.Max(0, newSpent);
        RefreshUI();
    }

    void CacheWizard()
    {
        cachedWizard = DataManager.Instance ? DataManager.Instance.GetWizard(playerIndex) : null;
        totalOrbs   = cachedWizard ? cachedWizard.loadoutOrbs : 0;

        if (elementalEmblem)
            elementalEmblem.sprite = cachedWizard ? cachedWizard.factionEmblem : null;
    }

    void RefreshUI()
    {
        int remaining = Mathf.Clamp(totalOrbs - spentOrbs, 0, Orbs.Length);

        for (int i = 0; i < Orbs.Length; i++)
            Orbs[i].SetActive(i < remaining);
    }
}
