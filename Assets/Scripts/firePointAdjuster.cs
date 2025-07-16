using UnityEngine;
using UnityEngine.Animations;

public class firePointAdjuster : MonoBehaviour
{
    public Transform gunFirePoint;
    public GunSwapControl gunControl;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gunControl = FindFirstObjectByType<GunSwapControl>();
    }

    // Update is called once per frame
    void Update()
    {
        gunFirePoint = transform.parent.Find("gunClone").GetChild(0);
        if (gunFirePoint != null)
            Debug.Log("Got it: " + gunFirePoint.name);
            transform.position = gunFirePoint.position;
        
    }
}
