using UnityEngine;
using UnityEngine.UI;

public class ButtonIconController : MonoBehaviour
{
    public Sprite[] buttonSprites;
    public float frameRate = 5f;
    private Image image;
    private int currentFrame;
    private float timer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1f / frameRate)
        {
            timer = 0f;
            currentFrame = (currentFrame + 1) % buttonSprites.Length;

            if (image != null)
                image.sprite = buttonSprites[currentFrame];
        }
    }
}
