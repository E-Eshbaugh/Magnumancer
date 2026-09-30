using UnityEngine;

/// One slab of the Bastion Stance shield: relays bullet hits for sparks and chips
public class BastionShieldPiece : MonoBehaviour, IBulletImpact
{
    BastionStance stance;
    public void Init(BastionStance owner) => stance = owner;
    public void OnBulletHit(Vector3 point, GameObject shooter)
    {
        if (stance != null) stance.Impact(point);
    }
}
