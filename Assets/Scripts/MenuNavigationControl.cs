using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuNavigationControl : MonoBehaviour
{
    [Header("-- Menu Pages --")]
    public GameObject page1;
    public GameObject page2;
    public GameObject page3;
    public GameObject page4;
    public GameObject page5;
    public GameObject page6;
    [Header("-- Book Opening Animation --")]
    public GameObject[] framesForward;
    public GameObject[] framesBackward;
    public float frameRate = 0.01f;
    private int currentBookIndex = 0;
    public bool hasStarted = false;
    void Start()
    {
        page3.SetActive(false);
        page4.SetActive(false);
        page5.SetActive(false);
        page6.SetActive(false);

        for (int i = 0; i < framesForward.Length; i++)
        {
            framesForward[i].SetActive(false);
        }

        for (int i = 0; i < framesBackward.Length; i++)
        {
            framesBackward[i].SetActive(false);
        }
    }

    void Update()
    {
        if (!hasStarted && Gamepad.current.aButton.wasPressedThisFrame)
        {
            if (page1.activeSelf)
            {
                //play page turn animation
                hasStarted = true;
                StartCoroutine(pageForwardAnimation());
                page1.SetActive(false);
                page2.SetActive(false);
                page3.SetActive(true);
                page4.SetActive(true);
                hasStarted = false;
            }
            else if (page3.activeSelf)
            {
                hasStarted = true;
                StartCoroutine(pageForwardAnimation());
                page3.SetActive(false);
                page4.SetActive(false);
                page5.SetActive(true);
                page6.SetActive(true);
                hasStarted = false;
            }
            else if (page5.activeSelf)
            {
                SceneManager.LoadScene("ForestMap");
            }
        }
        else if (Gamepad.current.bButton.wasPressedThisFrame)
        {
            if (page5.activeSelf)
            {
                //play page turn back animation
                hasStarted = true;
                StartCoroutine(pageBackwardAnimation());
                page6.SetActive(false);
                page5.SetActive(false);
                page4.SetActive(true);
                page3.SetActive(true);
                hasStarted = false;
            }
            else if (page3.activeSelf)
            {
                hasStarted = true;
                StartCoroutine(pageBackwardAnimation());
                page3.SetActive(false);
                page4.SetActive(false);
                page2.SetActive(true);
                page1.SetActive(true);
                hasStarted = false;
            }
            else if (page1.activeSelf)
            {
                //close book (?)
            }
        }
    }

    System.Collections.IEnumerator pageForwardAnimation()
    {
        framesForward[currentBookIndex].SetActive(true);
        while (currentBookIndex < framesForward.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            framesForward[currentBookIndex].SetActive(false);
            currentBookIndex++;
            framesForward[currentBookIndex].SetActive(true);
        }

        currentBookIndex = 0;
        for (int i = 0; i < framesForward.Length; i++)
        {
            framesForward[i].SetActive(false);
        }
    }

    System.Collections.IEnumerator pageBackwardAnimation()
    {
        framesBackward[currentBookIndex].SetActive(true);
        while (currentBookIndex < framesBackward.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            framesBackward[currentBookIndex].SetActive(false);
            currentBookIndex++;
            framesBackward[currentBookIndex].SetActive(true);
        }
        
        currentBookIndex = 0;
        for (int i = 0; i < framesBackward.Length; i++)
        {
            framesBackward[i].SetActive(false);
        }
    }
}
