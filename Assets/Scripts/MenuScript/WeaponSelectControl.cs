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
    [Tooltip("Old slide-in range. The bars now sit at maxX and fill left→right instead of sliding.")]
    public float minX = -614f;
    public float maxX = -211f;
    [Tooltip("How fast bars animate to a new weapon's stats (fraction per second)")]
    public float barFillSpeed = 4f;
    [Tooltip("Smallest visible fill so a low stat still shows a sliver")]
    [Range(0f, 0.2f)] public float minVisibleFill = 0.04f;

    // stat bar image -> target fill
    private readonly System.Collections.Generic.Dictionary<Image, float> barTargets = new();

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

        PrepareBar(weaponAmmo);
        PrepareBar(weaponDamage);
        PrepareBar(weaponAttackSpeed);
        PrepareBar(weaponWeight);

        UpdateTierList(true);
        UpdateWeaponDisplay();
    }

    void Update()
    {
        AnimateBars();

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

        // synergy notes depend on who's picking
        UpdateWeaponDisplay();

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
        weaponDescriptionText.text = wd.description + WeightLine(wd) + SynergyLine(wd);
        weaponIcon.sprite          = wd.weaponIcon;

        UpdateFillPercent(wd);
    }

    // "Synergy" note when this gun is one the picking wizard favours
    private string SynergyLine(WeaponData wd)
    {
        if (myWizard == null || !RuneBook.Favors(myWizard, wd)) return "";
        var a = RuneBook.AffinityOf(myWizard);
        // Color tag, not <b>: the description uses a bitmap font that can't do bold
        // in the picker's rune shade
        var shade = WizardShade.Shade(myWizard, DataManager.Instance.GetActiveRune(activePlayerIndex));
        string hex = ColorUtility.ToHtmlStringRGB(GlowLine.Brighten(shade));
        return $"\n<color=#{hex}>Synergy - {a.name}: {a.description}</color>";
    }

    // What the gun's weight will cost in the match, given what's already packed
    private string WeightLine(WeaponData wd)
    {
        int pack = 0;
        for (int i = 0; i < inventoryData.Length; i++)
        {
            var w = inventoryData[i];
            if (w != null && w != InventoryPlaceHolder && w != wd) pack += Mathf.Max(0, w.weight);
        }
        pack += Mathf.Max(0, wd.weight);

        // Granite Vow's Stonebind halves weight penalties
        bool stonebind = myWizard != null && myWizard.passive == PassiveType.Stonebind
                         && DataManager.Instance != null && DataManager.Instance.GetPassiveRune(activePlayerIndex) == 0;
        float speed = PlayerMovement3D.WeightSpeed(wd.weight, pack, stonebind ? 0.5f : 1f);
        int slow = Mathf.RoundToInt((1f - speed) * 100f);
        string feel = wd.weight >= 4 ? "Heavy" : wd.weight <= 1 ? "Light" : "Medium";
        return $"\n{feel} ({wd.weight}): {slow}% slower in hand - loadout weight {pack}";
    }

    private void UpdateFillPercent(WeaponData wd)
    {
        ammoPercent        = Mathf.Clamp01(wd.ammoCapacity   / 60f);
        // per trigger pull: shotgun damage is per pellet, so show the whole volley
        int perShot        = wd.isShotgun ? wd.damage * Mathf.Max(1, wd.pelletCount) : wd.damage;
        damagePercent      = Mathf.Clamp01(perShot           / 75f);
        attackSpeedPercent = Mathf.Clamp01(wd.attackSpeed    / 15f);
        weightPercent      = Mathf.Clamp01(wd.weight         / 5f);

        float range = maxX - minX;
        SetBar(weaponAmmo,        ammoPercent,        range);
        SetBar(weaponDamage,      damagePercent,      range);
        SetBar(weaponAttackSpeed, attackSpeedPercent, range);
        SetBar(weaponWeight,      weightPercent,      range);
    }

    // The bars used to "fill" by sliding the whole colored bar sideways and hiding the
    // overflow behind a big "Screen" image. That image stuck far outside the bar frame and
    // covered neighbouring UI. Now each bar stays put inside its frame and uses a
    // horizontal Filled image; the Screen occluders aren't needed.
    private void PrepareBar(RectTransform bar)
    {
        if (!bar) return;
        bar.anchoredPosition = new Vector2(maxX, bar.anchoredPosition.y);

        var img = bar.GetComponent<Image>();
        if (img != null)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            barTargets[img] = 0f;
        }

        if (bar.parent != null)
        {
            var screen = bar.parent.Find("Screen");
            if (screen != null) screen.gameObject.SetActive(false);
        }
    }

    private void SetBar(RectTransform bar, float pct, float range)
    {
        if (!bar) return;
        var img = bar.GetComponent<Image>();
        if (img == null || !barTargets.ContainsKey(img)) PrepareBar(bar);
        if (img != null) barTargets[img] = Mathf.Max(minVisibleFill, Mathf.Clamp01(pct));
    }

    private void AnimateBars()
    {
        foreach (var kv in barTargets)
        {
            if (kv.Key == null) continue;
            kv.Key.fillAmount = Mathf.MoveTowards(kv.Key.fillAmount, kv.Value, barFillSpeed * Time.unscaledDeltaTime);
        }
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
        UpdateWeaponDisplay(); // weight line depends on what's packed
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
