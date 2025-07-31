using UnityEngine;

public class ScrollUV : MonoBehaviour
{
    public float scrollSpeed = 1.5f;
    private Material mat;

    void Start() => mat = GetComponent<TrailRenderer>().material;
    void Update()
    {
        Vector2 offset = new Vector2(0, Time.time * scrollSpeed);
        mat.mainTextureOffset = offset;
    }
}
