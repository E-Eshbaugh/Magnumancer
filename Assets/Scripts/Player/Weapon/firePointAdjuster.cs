using UnityEngine;

public class FirePointAdjuster : MonoBehaviour
{
    public Transform gunFirePoint;
    public GunSwapControl gunControl;
    private Transform lastGun;

    void Start()
    {
        gunControl = GetComponentInParent<GunSwapControl>();
        if (gunControl == null)
        {
            Debug.LogError("[FirePointAdjuster] GunSwapControl not found on parent!");
        }
    }

    void Update()
    {
        if (gunControl == null) return;

        Transform gunClone = transform.parent.Find("gunClone");
        if (gunClone != lastGun)
        {
            lastGun = gunClone;
            if (lastGun != null && lastGun.childCount > 0)
            {
                gunFirePoint = lastGun.GetChild(0); // Or use lastGun.Find("FirePoint") if it has a named child
            }
            else
            {
                gunFirePoint = null;
                Debug.LogWarning("[FirePointAdjuster] gunClone missing or has no children.");
            }
        }

        if (gunFirePoint != null)
            transform.position = gunFirePoint.position;
    }
}
