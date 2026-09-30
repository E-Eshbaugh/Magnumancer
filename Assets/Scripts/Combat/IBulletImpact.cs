using UnityEngine;

/// Something that reacts when a bullet hits it (e.g. Bastion Stance's shield sparks)
public interface IBulletImpact
{
    void OnBulletHit(Vector3 point, GameObject shooter);
}
