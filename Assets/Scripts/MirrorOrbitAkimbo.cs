using UnityEngine;

[ExecuteAlways]
public class MirrorOrbitAkimbo : MonoBehaviour
{
    public Transform mainGunSlot;       // your primary weapon transform
    public Transform secondaryAnchor;   // the empty child for akimbo
    [Tooltip("Tweak this until the off-hand lines up perfectly")]
    public Vector3  rotationOffsetEuler = new Vector3(0, 0, 0);

    void LateUpdate()
    {
        if (mainGunSlot == null || secondaryAnchor == null) return;

        // Mirror position locally (assuming same parent pivot)
        Vector3 m = mainGunSlot.localPosition;
        secondaryAnchor.localPosition = new Vector3(-m.x, m.y, -m.z);

        // 1) Start with the main gun’s world rotation
        Quaternion worldRot = mainGunSlot.rotation;

        // 2) Flip 180° around world-up
        Quaternion flipped = worldRot * Quaternion.Euler(0f, 180f, 0f);

        // 3) Apply your small offset (in degrees) 
        Quaternion tweak = Quaternion.Euler(rotationOffsetEuler);

        // 4) Combine and assign
        secondaryAnchor.rotation = flipped * tweak;
    }
}

