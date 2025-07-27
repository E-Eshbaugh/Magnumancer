using UnityEngine;
using UnityEngine.InputSystem;

public class GunSwapControl : MonoBehaviour
{
    [Header("Controller")]
    public Gamepad gamepad;

    [Header("Gun Setup")]
    public Transform gunHolder;
    public Vector3 gunRotation;
    public GameObject[] gunPrefabs = new GameObject[4];

    // Runtime
    private AmmoControl ammoControl;
    public FireController3D fireController;
    private AkimboController akimboControl;

    private GameObject currentGun;
    public int currentGunIndex = 0;
    public WeaponData[] loadout;

    private bool isInitialized = false;

    void Awake()
    {
        ammoControl    = GetComponentInChildren<AmmoControl>();
        fireController = GetComponentInChildren<FireController3D>();
        akimboControl  = GetComponentInChildren<AkimboController>();
    }

    public void Setup(Gamepad pad, WeaponData[] srcLoadout)
    {
        gamepad = pad;
        loadout = srcLoadout != null ? (WeaponData[])srcLoadout.Clone() : new WeaponData[4];

        for (int i = 0; i < gunPrefabs.Length; i++)
            gunPrefabs[i] = (i < loadout.Length && loadout[i] != null) ? loadout[i].prefab : null;

        int first = FindFirstValidIndex();
        if (first >= 0) EquipGun(first);

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || gamepad == null) return;

        if (gamepad.dpad.up.wasPressedThisFrame && gunPrefabs[0] != null) EquipGun(0);
        if (gamepad.dpad.right.wasPressedThisFrame && gunPrefabs[1] != null) EquipGun(1);
        if (gamepad.dpad.down.wasPressedThisFrame && gunPrefabs[2] != null) EquipGun(2);
        if (gamepad.dpad.left.wasPressedThisFrame && gunPrefabs[3] != null) EquipGun(3);
    }

    void EquipGun(int index)
    {
        if (index < 0 || index >= gunPrefabs.Length || gunPrefabs[index] == null) return;

        if (currentGun) Destroy(currentGun);

        currentGunIndex = index;
        currentGun = Instantiate(gunPrefabs[index], gunHolder);
        currentGun.transform.localPosition = Vector3.zero;
        currentGun.transform.localRotation = Quaternion.Euler(gunRotation);
        currentGun.name = "gunClone";

        ammoControl?.OnGunEquipped(index);

        if (akimboControl)
        {
            akimboControl.EndAkimbo();
            akimboControl.secondaryAmmo = 0;
        }
    }

    int FindFirstValidIndex()
    {
        for (int i = 0; i < gunPrefabs.Length; i++)
            if (gunPrefabs[i] != null) return i;
        return -1;
    }
}
