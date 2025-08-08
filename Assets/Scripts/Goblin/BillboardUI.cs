using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180f, 0); // Face the camera fully
        }
    }
}
