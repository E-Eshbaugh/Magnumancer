using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinManager : MonoBehaviour
{
    [Tooltip("All the tags you use on your player GameObjects")]
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    private bool endSequenceStarted = false;
    private int maxAliveSeen = 0;
    GoblinSpawner horde;

    void Start() => horde = FindAnyObjectByType<GoblinSpawner>();

    void Update()
    {
        int standingCount = 0;
        foreach (var player in PlayerHealthControl.ActivePlayers)
            if (player.IsStanding) standingCount++;
        maxAliveSeen = Mathf.Max(maxAliveSeen, standingCount);

        // Co-op continues with one rescuer left; only a full team wipe ends the run.
        bool wavesCleared = Teams.HumansVsHorde && horde != null && horde.CurrentWave > 0 && horde.AllWavesComplete;
        bool ended = wavesCleared || (Teams.HumansVsHorde
            ? standingCount == 0 && maxAliveSeen > 0
            : (standingCount <= 1 && maxAliveSeen >= 2) || (standingCount == 0 && maxAliveSeen > 0));
        if (!endSequenceStarted && ended)
        {
            endSequenceStarted = true;
            if (wavesCleared || !Teams.HumansVsHorde)
                foreach (var winner in PlayerHealthControl.ActivePlayers)
                    if (winner.IsStanding) Rumble.Play(winner.gameObject, 0.6f, 1f, 1.2f);
            StartCoroutine(WaitAndReturnToMainMenu());
        }
    }

    private IEnumerator WaitAndReturnToMainMenu()
    {
        // (optional) show a "You Win!" UI here before the wait
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene(MenuScenes.Hall);
    }
}
