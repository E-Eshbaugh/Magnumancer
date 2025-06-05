using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BookOpen : MonoBehaviour
{
    public GameObject[] books;
    public Text bookText;
    public Text bookText2;
    public float frameRate = 0.01f;

    private int currentBookIndex = 0;
    private bool hasStarted = false;

    void Start()
    {
        for (int i = 0; i < books.Length; i++)
        {
            books[i].SetActive(i == 0);
        }
    }

    void Update()
    {
        if (!hasStarted && Gamepad.current != null && Gamepad.current.aButton.IsPressed())
        {
            hasStarted = true;
            StartCoroutine(PlayBookOpening());
        }
    }

    System.Collections.IEnumerator PlayBookOpening()
    {
        bookText.text = "";
        bookText2.text = "";

        while (currentBookIndex < books.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            books[currentBookIndex].SetActive(false);
            currentBookIndex++;
            books[currentBookIndex].SetActive(true);
        }
    }
}
