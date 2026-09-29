using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    private Transform camTransform;

    // Offset currently applied on top of wherever other scripts (e.g. ScrollingTrackCamera)
    // put the camera. Shaking is additive so it never pins the camera to a stale position.
    private Vector3 appliedOffset;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(this); return; } // remove the duplicate component, not the camera

        camTransform = Camera.main.transform;
    }

    public static void Shake(float intensity, float duration)
    {
        if (instance == null) return;
        instance.StopAllCoroutines();
        instance.ClearOffset();
        instance.StartCoroutine(instance.DoShake(intensity, duration));
    }

    private IEnumerator DoShake(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 offset = Random.insideUnitSphere * intensity;
            camTransform.localPosition += offset - appliedOffset;
            appliedOffset = offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearOffset();
    }

    private void ClearOffset()
    {
        if (camTransform) camTransform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;
    }
}
