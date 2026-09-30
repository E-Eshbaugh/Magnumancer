using UnityEngine;

/// Cleans up a runtime material instance with its owner
public class DestroyMaterialOnDestroy : MonoBehaviour
{
    public Material material;
    void OnDestroy() { if (material != null) Destroy(material); }
}
