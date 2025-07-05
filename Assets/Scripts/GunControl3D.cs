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
    private FireController3D fireController;
    private AkimboController akimboControl;

    private GameObject currentGun;
    public  int        currentGunIndex = 0;

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
        EquipGun(currentGunIndex);
    }

    void Update()
    {
        //gunHolder.rotation = Quaternion.Euler(gunRotation);
        if (gamepad == null) return;

        if (gamepad.rightShoulder.wasPressedThisFrame) CycleGun(+1);
        if (gamepad.leftShoulder .wasPressedThisFrame) CycleGun(-1);
    }

    void CycleGun(int delta)
    {
        int len = gunPrefabs.Length;
        currentGunIndex = (currentGunIndex + delta + len) % len;
        EquipGun(currentGunIndex);
    }

    void EquipGun(int index)
    {
        // destroy old
        if (currentGun != null) Destroy(currentGun);

        // instantiate new
        currentGun = Instantiate(gunPrefabs[index], gunHolder);
        currentGun.transform.localPosition = Vector3.zero;
        currentGun.transform.localRotation = Quaternion.Euler(gunRotation);

        // re-target the shooter
        if (fireController != null)
        {
            fireController.firePoint = gunHolder;
        }

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

