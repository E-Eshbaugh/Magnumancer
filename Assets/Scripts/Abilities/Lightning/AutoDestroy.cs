using UnityEngine;
public class AutoDestroy : MonoBehaviour
{
    public float lifetime = 0.1f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
