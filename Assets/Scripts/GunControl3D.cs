using UnityEngine;
using UnityEngine.InputSystem;

public class GunSwapControl : MonoBehaviour
{

    public Transform gunHolder;
    public GameObject[] gunPrefabs;

    private GameObject currentGun;
    public Vector3 gunRotation;
    public AmmoControl ammoControl;
    public AkimboController akimboControl;
    public int currentGunIndex = 0;
    public int gunPrefabsLength;

    public void EquipGun(int index)
    {
        if (currentGun != null)
            Destroy(currentGun);

        currentGun = Instantiate(gunPrefabs[index], gunHolder);
        currentGun.transform.localPosition = Vector3.zero;
        currentGun.transform.localRotation = Quaternion.Euler(gunRotation);
        //reset ammo type to base ammo type on switch
        if (ammoControl != null)
        {
            ammoControl.currentGunIndex = index;
            ammoControl.currentGun = ammoControl.guns[index];
            ammoControl.ammoCount = ammoControl.currentGun.ammoCapacity;
            ammoControl.currentGun.ammoType = ammoControl.currentGun.baseAmmoType; // reset to base ammo type
            akimboControl.EndAkimbo(); // reset akimbo state
            akimboControl.secondaryAmmo = 0;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EquipGun(currentGunIndex);
        gunPrefabsLength = gunPrefabs.Length;
    }

    // Update is called once per frame
    void Update()
    {

        if (Gamepad.current.rightShoulder.wasPressedThisFrame)
        {
            currentGunIndex = currentGunIndex + 1;
            if (currentGunIndex == gunPrefabs.Length)
            {
                currentGunIndex = 0;
            }

            EquipGun(currentGunIndex);
        }

        if (Gamepad.current.leftShoulder.wasPressedThisFrame)
        {
            currentGunIndex = currentGunIndex - 1;
            if (currentGunIndex < 0)
            {
                currentGunIndex = gunPrefabs.Length - 1;
            }

            EquipGun(currentGunIndex);
        }
    }
}
