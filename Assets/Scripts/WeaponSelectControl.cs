using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class WeaponSelectControl : MonoBehaviour
{
    public TierTextControl tierTextControl;
    [Header("-- Controller Settings --")]
    public float stickThreshold = 0.5f;
    public float inputCooldown = 0.25f;
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
    [Header("-- Archon Weapons --")]
    public WeaponData[] archonWeapons;
    [Header("-- Ascendant Weapons --")]
    public WeaponData[] ascendantWeapons;
    [Header("-- Initiate Weapons --")]
    public WeaponData[] initiateWeapons;
    [Header("-- Orbs --")]
    public MAgicManagement magicManagement;
    [Header("-- Inventory Settings --")]
    [Tooltip("0 - UP | 1 - RIGHT | 2 - DOWN | 3 - LEFT")]
    public Image[] inventorySlots = new Image[4];
    public WeaponData[] inventoryData = new WeaponData[4];
    public WeaponData InventoryPlaceHolder;
    public bool clear = false;
    private float minX = -614f; //5%
    private float maxX = -211f; //99%


    private Array currentWeaponArray;
    private int currentWeaponIndex = 0;
    private int prevTier = 0;
    public float ammoPercent = 0;
    private float damagePercent = 0;
    private float attackSpeedPercent = 0;
    private float weightPercent = 0;
    void Start()
    {
        currentWeaponArray = initiateWeapons;
        tierTextControl = FindFirstObjectByType<TierTextControl>();
        magicManagement = FindFirstObjectByType<MAgicManagement>();

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlots[i].enabled = false;
        }

        UpdateWeaponDisplay();
    }

    // Update is called once per frame
    void Update()
    {
        //get new tier from tierTextControl (reset currentWeaponIndex if changed)
        if (tierTextControl.currentTier == 0)
        {
            currentWeaponArray = initiateWeapons;
            if (prevTier != tierTextControl.currentTier)
            {
                currentWeaponIndex = 0;
                prevTier = tierTextControl.currentTier;
                UpdateWeaponDisplay();
            }
        }
        else if (tierTextControl.currentTier == 1)
        {
            currentWeaponArray = ascendantWeapons;
            if (prevTier != tierTextControl.currentTier)
            {
                currentWeaponIndex = 0;
                prevTier = tierTextControl.currentTier;
                UpdateWeaponDisplay();
            }
        }
        else if (tierTextControl.currentTier == 2)
        {
            currentWeaponArray = archonWeapons;
            if (prevTier != tierTextControl.currentTier)
            {
                currentWeaponIndex = 0;
                prevTier = tierTextControl.currentTier;
                UpdateWeaponDisplay();
            }
        }

        // Handle input for weapon scrolling
        float x = Gamepad.current.rightStick.ReadValue().x;
        float time = Time.time;

        if (time - lastInputTime > inputCooldown)
        {
            if (x > stickThreshold)
            {
                ScrollRight();
                lastInputTime = time;
            }
            else if (x < -stickThreshold)
            {
                ScrollLeft();
                lastInputTime = time;
            }
        }

        //weapon selection with DPad to put in inventory slots

        if (Gamepad.current.buttonWest.IsPressed())
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame)
            {
                ClearWeapon(0);
            }
            else if (Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                ClearWeapon(1);
                clear = true;
            }
            else if (Gamepad.current.dpad.down.wasPressedThisFrame)
            {
                ClearWeapon(2);
            }
            else if (Gamepad.current.dpad.left.wasPressedThisFrame)
            {
                ClearWeapon(3);
            }
        }
        else if (Gamepad.current.dpad.up.wasPressedThisFrame)
        {
            SelectWeapon(0);
        }
        else if (Gamepad.current.dpad.right.wasPressedThisFrame)
        {
            SelectWeapon(1);
        }
        else if (Gamepad.current.dpad.down.wasPressedThisFrame)
        {
            SelectWeapon(2);
        }
        else if (Gamepad.current.dpad.left.wasPressedThisFrame)
        {
            SelectWeapon(3);
        }
    }

    void ScrollRight()
    {
        currentWeaponIndex = currentWeaponIndex + 1;
        if (currentWeaponIndex >= currentWeaponArray.Length)
        {
            currentWeaponIndex = 0;
        }

        UpdateWeaponDisplay();
    }

    void ScrollLeft()
    {
        currentWeaponIndex = currentWeaponIndex - 1;
        if (currentWeaponIndex < 0)
        {
            currentWeaponIndex = currentWeaponArray.Length - 1;
        }

        UpdateWeaponDisplay();
    }

    void UpdateWeaponDisplay()
    {
        weaponNameText.text = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weaponName;
        weaponOrbCostText.text = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).orbCost.ToString();
        weaponDescriptionText.text = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).description;
        weaponIcon.sprite = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weaponIcon;

        //stats
        //damage [0-50]
        //ammo [0-60]
        //rof [0-15]
        //weight [0-5]
        UpdateFillPercent();

    }

    void UpdateFillPercent()
    {
        ammoPercent = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).ammoCapacity / 60f;
        damagePercent = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).damage / 40f;
        attackSpeedPercent = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).attackSpeed / 15f;
        weightPercent = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weight / 5f;

        float fillBarRange = maxX - minX;

        //ammo
        float ammoX = minX + (ammoPercent * fillBarRange);
        if (ammoX < minX) ammoX = minX;
        if (ammoX > maxX) ammoX = maxX;
        weaponAmmo.anchoredPosition = new Vector2(ammoX, weaponAmmo.anchoredPosition.y);

        //damage
        float damageX = minX + (damagePercent * fillBarRange);
        if (damageX < minX) damageX = minX;
        if (damageX > maxX) damageX = maxX;
        weaponDamage.anchoredPosition = new Vector2(damageX, weaponDamage.anchoredPosition.y);

        //attack speed
        float attackSpeedX = minX + (attackSpeedPercent * fillBarRange);
        if (attackSpeedX < minX) attackSpeedX = minX;
        if (attackSpeedX > maxX) attackSpeedX = maxX;
        weaponAttackSpeed.anchoredPosition = new Vector2(attackSpeedX, weaponAttackSpeed.anchoredPosition.y);

        //weight
        float weightX = minX + (weightPercent * fillBarRange);
        if (weightX < minX) weightX = minX;
        if (weightX > maxX) weightX = maxX;
        weaponWeight.anchoredPosition = new Vector2(weightX, weaponWeight.anchoredPosition.y);
    }

    void SelectWeapon(int slotIndex)
    {
        int remainingOrbs = magicManagement.numOrbs - ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).orbCost;

        //already in inventory?
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i].enabled && inventorySlots[i].sprite == ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weaponIcon)
            {
                return;
            }
        }

        if (remainingOrbs >= 0)
        {
            inventorySlots[slotIndex].sprite = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weaponIcon;
            inventorySlots[slotIndex].enabled = true;
            inventoryData[slotIndex] = (WeaponData)currentWeaponArray.GetValue(currentWeaponIndex);
            magicManagement.spentOrbs = CountInventoryCost();
        }
        else
        {
            if (inventorySlots[slotIndex].enabled)
            {
                int swappedPrice = remainingOrbs + ((WeaponData)inventoryData.GetValue(slotIndex)).orbCost;
                if (swappedPrice >= 0)
                {
                    inventorySlots[slotIndex].sprite = ((WeaponData)currentWeaponArray.GetValue(currentWeaponIndex)).weaponIcon;
                    inventorySlots[slotIndex].enabled = true;
                    inventoryData[slotIndex] = (WeaponData)currentWeaponArray.GetValue(currentWeaponIndex);
                    magicManagement.spentOrbs = CountInventoryCost();
                }
                else return;
            }
            else return;
        }

    }

    void ClearWeapon(int slotIndex)
    {
        if (inventorySlots[slotIndex].enabled)
        {
            inventorySlots[slotIndex].enabled = false;
            inventoryData[slotIndex] = InventoryPlaceHolder;
            magicManagement.spentOrbs = CountInventoryCost();
        }
        else return;
    }

    int CountInventoryCost()
    {
        int orbCount = 0;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            orbCount += ((WeaponData)inventoryData.GetValue(i)).orbCost;
        }

        return orbCount;
    }
}
