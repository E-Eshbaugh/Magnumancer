using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinManager : MonoBehaviour
{
    [Tooltip("All the tags you use on your player GameObjects")]
    public string[] playerTags = { "Player1", "Player2", "Player3", "Player4" };

    private bool endSequenceStarted = false;

    void Update()
    {
        // tally up how many players remain
        int aliveCount = 0;
        foreach (var tag in playerTags)
            aliveCount += GameObject.FindGameObjectsWithTag(tag).Length;

        // once only one is left, start the end sequence
        if (!endSequenceStarted && aliveCount == 1)
        {
            endSequenceStarted = true;
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
