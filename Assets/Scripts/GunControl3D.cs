using UnityEngine;
using UnityEngine.InputSystem;

public class GunSwapControl : MonoBehaviour
{
    [Header("Controller")]
    public Gamepad gamepad;                // set by MultiplayerManager

    [Header("Gun Setup")]
    public Transform   gunHolder;
    public GameObject[] gunPrefabs;
    public Vector3      gunRotation;

    // we’ll fill these in Awake() so you don’t have to hook them up manually
    private AmmoControl      ammoControl;
    public FireController3D fireController;
    private AkimboController akimboControl;

    private GameObject currentGun;
    public  int        currentGunIndex = 0;
    public WeaponData[] loadout; // 0 - UP | 1 - RIGHT | 2 - DOWN | 3 - LEFT

    void Awake()
    {
        // Find these components on this object or any of its children:
        ammoControl    = GetComponentInChildren<AmmoControl>();
        fireController = GetComponentInChildren<FireController3D>();
        akimboControl  = GetComponentInChildren<AkimboController>();

        if (ammoControl    == null) Debug.LogWarning("GunSwapControl: no AmmoControl found in children!");
        if (fireController == null) Debug.LogWarning("GunSwapControl: no FireController3D found in children!");
        if (akimboControl  == null) Debug.LogWarning("GunSwapControl: no AkimboController found in children!");
    }


    void Start()
    {
        loadout = DataManager.Instance.loadout;
        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] == null)
            {
                Debug.LogWarning($"GunSwapControl: loadout[{i}] is null, skipping.");
                continue;
            }
            else gunPrefabs[i] = loadout[i].prefab;

        }

        if (gunPrefabs[currentGunIndex] == null)
        {
            return;
        }
        EquipGun(currentGunIndex);
    }

    void Update()
    {
        //gunHolder.rotation = Quaternion.Euler(gunRotation);
        if (gamepad == null) return;

        if (gamepad.dpad.up.wasPressedThisFrame && gunPrefabs[0] != null) EquipGun(0);
        if (gamepad.dpad.right.wasPressedThisFrame && gunPrefabs[1] != null) EquipGun(1);
        if (gamepad.dpad.down.wasPressedThisFrame && gunPrefabs[2] != null) EquipGun(2);
        if (gamepad.dpad.left.wasPressedThisFrame && gunPrefabs[3] != null) EquipGun(3);
    }

    void EquipGun(int index)
    {
        // destroy old
        if (currentGun != null) Destroy(currentGun);

        currentGunIndex = index;

        // instantiate new
        currentGun = Instantiate(gunPrefabs[index], gunHolder);
        currentGun.transform.localPosition = Vector3.zero;
        currentGun.transform.localRotation = Quaternion.Euler(gunRotation);
        currentGun.name = "gunClone";

        // reset ammo
        if (ammoControl != null)
        {
            ammoControl.OnGunEquipped(index);
        }

        // reset akimbo
        if (akimboControl != null)
        {
            akimboControl.EndAkimbo();
            akimboControl.secondaryAmmo = 0;
        }
    }
}

