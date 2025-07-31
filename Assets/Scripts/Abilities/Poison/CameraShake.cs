using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    private Transform camTransform;
    private Vector3 originalPos;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        camTransform = Camera.main.transform;
        originalPos = camTransform.localPosition;
    }

    public static void Shake(float intensity, float duration)
    {
        if (instance == null) return;
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.DoShake(intensity, duration));
    }

    private IEnumerator DoShake(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 offset = Random.insideUnitSphere * intensity;
            camTransform.localPosition = originalPos + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        camTransform.localPosition = originalPos;
    }
}
