using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScrollingText : MonoBehaviour
{
    public RectTransform textRect;
    public float scrollSpeed = 30f;
    public float delayBeforeStart = 1f;

    private Vector2 startPos;
    private float endY = 5406;

    void Start()
    {
        startPos = textRect.anchoredPosition;
        StartCoroutine(ScrollUp());
    }

    System.Collections.IEnumerator ScrollUp()
    {
        yield return new WaitForSeconds(delayBeforeStart);

        while (textRect.anchoredPosition.y < endY)
        {
            textRect.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
            yield return null;
        }
    }
}
