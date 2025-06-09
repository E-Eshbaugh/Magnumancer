using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FireBurnEffect : MonoBehaviour
{
    public Sprite[] framesStart;
    public Sprite[] framesEnd;
    public Image screen;
    public float frameRate = 0.5f;
    public GameObject scroll;
    public BookOpen bookOpen;
    private int currentFrameIndex = 0;
    private bool hasStarted = false;

    void Start()
    {
        screen.enabled = false;
        bookOpen = FindFirstObjectByType<BookOpen>();
    }

    void Update()
    {
        if (!hasStarted && Gamepad.current != null && Gamepad.current.aButton.IsPressed())
        {
            screen.enabled = true;
            hasStarted = true;
            StartCoroutine(PlayAnimationStart());
        }
    }

    System.Collections.IEnumerator PlayAnimationStart()
    {
        while (currentFrameIndex < framesStart.Length)
        {
            yield return new WaitForSeconds(frameRate);
            screen.sprite = framesStart[currentFrameIndex];
            currentFrameIndex++;
        }

        currentFrameIndex = 0;
        scroll.SetActive(false);
        StartCoroutine(PlayAnimationEnd());
        StartCoroutine(bookOpen.PlayBookOpening());
    }

    System.Collections.IEnumerator PlayAnimationEnd()
    {
        while (currentFrameIndex < framesEnd.Length)
        {
            yield return new WaitForSeconds(frameRate);
            screen.sprite = framesEnd[currentFrameIndex];
            currentFrameIndex++;
        }

        screen.enabled = false;
    }
}
