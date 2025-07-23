using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponSelectControl : MonoBehaviour
{
    [Header("-- External Refs --")]
    public TierTextControl tierTextControl;
    public MagicManagement magicManagement;               // UI for orbs
    public Gamepad activePad;                              // set by MenuNavigationControl
    public int activePlayerIndex = 0;

    [Header("-- Controller Settings --")]
    public float stickThreshold = 0.5f;
    public float inputCooldown  = 0.25f;
    private float lastInputTime = 0f;

    [Header("-- UI --")]
    public Text weaponNameText;
    public Text weaponDescriptionText;
    public Text weaponOrbCostText;
    public RectTransform weaponDamage;
    public RectTransform weaponAmmo;
    public RectTransform weaponAttackSpeed;
    public RectTransform weaponWeight;
    public Image weaponIcon;

    [Header("-- Weapons by Tier --")]
    public WeaponData[] initiateWeapons;
    public WeaponData[] ascendantWeapons;
    public WeaponData[] archonWeapons;

    [Header("-- Inventory Settings --")]
    [Tooltip("0 - UP | 1 - RIGHT | 2 - DOWN | 3 - LEFT")]
    public Image[]     inventorySlots = new Image[4];
    public WeaponData[] inventoryData  = new WeaponData[4];
    public WeaponData   InventoryPlaceHolder;

    [Header("-- Fillbar Settings --")]
    [Tooltip("Anchored X positions (min->max) for stat bars")]
    public float minX = -614f;
    public float maxX = -211f;

    // Runtime
    private WeaponData[] currentList;
    private int currentWeaponIndex = 0;
    private int prevTier = -1;

    // Wizard info
    private WizardData myWizard;
    private int totalOrbs;

    // cached stat percents (debug only)
    public float ammoPercent        { get; private set; }
    public float damagePercent      { get; private set; }
    public float attackSpeedPercent { get; private set; }
    public float weightPercent      { get; private set; }

    #region Unity
    void Start()
    {
        if (!tierTextControl)  tierTextControl  = FindFirstObjectByType<TierTextControl>();
        if (!magicManagement)  magicManagement  = FindFirstObjectByType<MagicManagement>();

        // Init inventory placeholders
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i]) inventorySlots[i].enabled = false;
            inventoryData[i] = InventoryPlaceHolder;
        }

        UpdateTierList(true);
        UpdateWeaponDisplay();
    }

    void Update()
    {
        if (activePad == null) return;

        // 1) Tier change?
        UpdateTierList(false);

        // 2) Horizontal scroll
        float x = activePad.rightStick.ReadValue().x;
        float time = Time.time;

        if (time - lastInputTime > inputCooldown)
        {
            if (x > stickThreshold)       { ScrollRight(); lastInputTime = time; }
            else if (x < -stickThreshold) { ScrollLeft();  lastInputTime = time; }
        }

        // 3) Inventory input
        bool clearMode = activePad.buttonWest.isPressed; // hold X to clear

        if (activePad.dpad.up.wasPressedThisFrame)        HandleSlotInput(0, clearMode);
        else if (activePad.dpad.right.wasPressedThisFrame) HandleSlotInput(1, clearMode);
        else if (activePad.dpad.down.wasPressedThisFrame)  HandleSlotInput(2, clearMode);
        else if (activePad.dpad.left.wasPressedThisFrame)  HandleSlotInput(3, clearMode);
    }
    #endregion

    #region Public API
    /// Called by MenuNavigationControl when this player starts picking.
    public void SetActivePlayer(int playerIndex, Gamepad pad)
    {
        activePlayerIndex = playerIndex;
        activePad         = pad;

        // Cache wizard + orbs
        myWizard  = DataManager.Instance.GetWizard(playerIndex);
        totalOrbs = myWizard ? myWizard.loadoutOrbs : 0;

        // sync MAgicManagement
        if (magicManagement) magicManagement.SetPlayer(playerIndex);

        // Optional: ResetSelection();
    }

    public void ResetSelection()
    {
        currentWeaponIndex = 0;
        prevTier = -1;
        UpdateTierList(true);
        UpdateWeaponDisplay();

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i]) inventorySlots[i].enabled = false;
            inventoryData[i] = InventoryPlaceHolder;
        }
        PushSpentToUI();
    }
    #endregion

    #region Input Helpers
    private void HandleSlotInput(int slotIndex, bool clearMode)
    {
        if (clearMode) ClearWeapon(slotIndex);
        else           SelectWeapon(slotIndex);
    }

    private void ScrollRight()
    {
        if (currentList == null || currentList.Length == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % currentList.Length;
        UpdateWeaponDisplay();
    }

    private void ScrollLeft()
    {
        if (currentList == null || currentList.Length == 0) return;
        currentWeaponIndex--;
        if (currentWeaponIndex < 0) currentWeaponIndex = currentList.Length - 1;
        UpdateWeaponDisplay();
    }
    #endregion

    #region Tier & Display
    private void UpdateTierList(bool force)
    {
        int tier = tierTextControl ? tierTextControl.currentTier : 0;
        if (!force && tier == prevTier) return;

        switch (tier)
        {
            case 0: currentList = initiateWeapons;  break;
            case 1: currentList = ascendantWeapons; break;
            case 2: currentList = archonWeapons;    break;
            default: currentList = initiateWeapons;  break;
        }

        currentWeaponIndex = 0;
        prevTier = tier;

        UpdateWeaponDisplay();
    }

    private void UpdateWeaponDisplay()
    {
        if (currentList == null || currentList.Length == 0)
        {
            weaponNameText.text        = "---";
            weaponOrbCostText.text     = "0";
            weaponDescriptionText.text = "No weapons";
            weaponIcon.sprite          = null;
            return;
        }

        WeaponData wd = currentList[currentWeaponIndex];

        weaponNameText.text        = wd.weaponName;
        weaponOrbCostText.text     = wd.orbCost.ToString();
        weaponDescriptionText.text = wd.description;
        weaponIcon.sprite          = wd.weaponIcon;

        UpdateFillPercent(wd);
    }

    private void UpdateFillPercent(WeaponData wd)
    {
        ammoPercent        = Mathf.Clamp01(wd.ammoCapacity   / 60f);
        damagePercent      = Mathf.Clamp01(wd.damage         / 40f);
        attackSpeedPercent = Mathf.Clamp01(wd.attackSpeed    / 15f);
        weightPercent      = Mathf.Clamp01(wd.weight         / 5f);

        float range = maxX - minX;
        SetBar(weaponAmmo,        ammoPercent,        range);
        SetBar(weaponDamage,      damagePercent,      range);
        SetBar(weaponAttackSpeed, attackSpeedPercent, range);
        SetBar(weaponWeight,      weightPercent,      range);
    }

    private void SetBar(RectTransform bar, float pct, float range)
    {
        if (!bar) return;
        float x = Mathf.Clamp(minX + pct * range, minX, maxX);
        bar.anchoredPosition = new Vector2(x, bar.anchoredPosition.y);
    }
    #endregion

    #region Inventory
    private void SelectWeapon(int slotIndex)
    {
        if (!SlotIndexValid(slotIndex)) return;
        if (currentList == null || currentList.Length == 0) return;

        WeaponData chosen = currentList[currentWeaponIndex];
        if (chosen == null) return;

        // block duplicates
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i].enabled && inventoryData[i] == chosen)
                return;
        }

        int currentCost = CountInventoryCost();
        int afterAdd    = currentCost + chosen.orbCost;

        if (afterAdd <= totalOrbs)
        {
            ApplySlot(slotIndex, chosen);
        }
        else
        {
            // try swap
            if (inventorySlots[slotIndex].enabled)
            {
                int refund   = (inventoryData[slotIndex] != null && inventoryData[slotIndex] != InventoryPlaceHolder)
                                ? inventoryData[slotIndex].orbCost : 0;
                int afterSwap = currentCost - refund + chosen.orbCost;

                if (afterSwap <= totalOrbs)
                    ApplySlot(slotIndex, chosen);
            }
        }
    }

    private void ClearWeapon(int slotIndex)
    {
        if (!SlotIndexValid(slotIndex)) return;

        if (inventorySlots[slotIndex].enabled)
        {
            inventorySlots[slotIndex].enabled = false;
            inventorySlots[slotIndex].sprite  = null;
            inventoryData[slotIndex]          = InventoryPlaceHolder;
            PushSpentToUI();
        }
    }

    private void ApplySlot(int slotIndex, WeaponData data)
    {
        inventorySlots[slotIndex].sprite  = data.weaponIcon;
        inventorySlots[slotIndex].enabled = true;
        inventoryData[slotIndex]          = data;
        PushSpentToUI();
    }

    private int CountInventoryCost()
    {
        int cost = 0;
        for (int i = 0; i < inventoryData.Length; i++)
        {
            var w = inventoryData[i];
            if (w != null && w != InventoryPlaceHolder)
                cost += w.orbCost;
        }
        return cost;
    }

    private bool SlotIndexValid(int idx) => idx >= 0 && idx < inventorySlots.Length;

    private void PushSpentToUI()
    {
        int spent = CountInventoryCost();
        if (magicManagement != null)
        {
            // If you implemented OnSpentChanged
            if (magicManagement.GetType().GetMethod("OnSpentChanged") != null)
                magicManagement.OnSpentChanged(spent);
            else
                magicManagement.spentOrbs = spent; // legacy fallback
        }
    }
    #endregion
}
