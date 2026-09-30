using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinManager : MonoBehaviour
{
    [Tooltip("All the tags you use on your player GameObjects")]
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    private bool endSequenceStarted = false;
    private int maxAliveSeen = 0;

    void Update()
    {
        // tally up how many players remain
        int aliveCount = 0;
        foreach (var tag in playerTags)
            aliveCount += GameObject.FindGameObjectsWithTag(tag).Length;
        maxAliveSeen = Mathf.Max(maxAliveSeen, aliveCount);

        // once only one is left, start the end sequence
        // (only if the match actually had 2+ players, so solo testing doesn't end instantly)
        if (!endSequenceStarted && aliveCount == 1 && maxAliveSeen >= 2)
        {
            endSequenceStarted = true;

            // the last wizard standing gets a victory rumble
            foreach (var tag in playerTags)
                foreach (var winner in GameObject.FindGameObjectsWithTag(tag))
                    Rumble.Play(winner, 0.6f, 1f, 1.2f);
            StartCoroutine(WaitAndReturnToMainMenu());
        }
    }

    private IEnumerator WaitAndReturnToMainMenu()
    {
        // (optional) show a "You Win!" UI here before the wait
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene("MainMenu");
    }
}
