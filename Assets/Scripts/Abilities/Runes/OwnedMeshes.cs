using System.Collections.Generic;
using UnityEngine;

/// Frees runtime-generated meshes along with the object that owns them
public class OwnedMeshes : MonoBehaviour
{
    public readonly List<Mesh> meshes = new List<Mesh>();
    void OnDestroy() { foreach (var m in meshes) if (m != null) Destroy(m); }
}
