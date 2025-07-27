using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BookOpen : MonoBehaviour
{
    public GameObject menu;
    public GameObject[] books;
    public Text bookText;
    public GameObject bookText2;
    public float frameRate = 0.01f;

    private int currentBookIndex = 0;

    void Start()
    {
        menu.SetActive(false);

        for (int i = 0; i < books.Length; i++)
        {
            books[i].SetActive(i == 0);
        }
    }

    void Update()
    {

    }

    public System.Collections.IEnumerator PlayBookOpening()
    {
        bookText.text = "";
        bookText2.SetActive(false);

        while (currentBookIndex < books.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            books[currentBookIndex].SetActive(false);
            currentBookIndex++;
            books[currentBookIndex].SetActive(true);
        }

        menu.SetActive(true);
    }
}
