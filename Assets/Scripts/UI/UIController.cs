using UnityEngine;

public class UIController : MonoBehaviour
{
    public GameObject UI;
    public GameObject Player;

    PlayerHealthControl health;

    // Shows the HUD for players in the match. Eliminated players' HUDs stay up, greyed out.
    void Update()
    {
        if (Player == null || UI == null) return;
        if (health == null) health = Player.GetComponentInChildren<PlayerHealthControl>(true);
        bool eliminated = health != null && health.IsDead;
        UI.SetActive(Player.activeSelf || eliminated);
    }
}
