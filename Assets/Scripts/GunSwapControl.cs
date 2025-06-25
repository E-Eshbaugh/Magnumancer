using UnityEngine;

public class GunSwapControl : MonoBehaviour
{

    public Transform gunHolder;
    public GameObject[] gunPrefabs;

    private GameObject currentGun;
    public Vector3 gunRotation;

    public void EquipGun(int index)
    {
        if (currentGun != null)
            Destroy(currentGun);

        currentGun = Instantiate(gunPrefabs[index], gunHolder);
        currentGun.transform.localPosition = Vector3.zero;
        currentGun.transform.localRotation = Quaternion.Euler(gunRotation);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EquipGun(0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
