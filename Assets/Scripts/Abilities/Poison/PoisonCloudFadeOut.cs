using UnityEngine;
using System.Collections;

public class PoisonCloudFadeOut : MonoBehaviour
{
    [Header("Fade Settings")]
    public float activeDuration = 10f;          // How long it emits
    public float destroyDelay = 3f;             // Time after stopping to clean up

    private ParticleSystem[] particleSystems;

    void Start()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>();
        StartCoroutine(StopEmissionAfterDelay());
    }

    private IEnumerator StopEmissionAfterDelay()
    {
        yield return new WaitForSeconds(activeDuration);

        foreach (ParticleSystem ps in particleSystems)
        {
            // THIS is the key fix
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
