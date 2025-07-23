using UnityEngine;
using UnityEngine.UI;

public class ModeImageChanger : MonoBehaviour
{
    public Sprite[] modeImages;
    public GameObject onlineModePage;
    public GameObject localModePage;
    public ModeIconSelectScript modeIconSelectScript;
    public Image imagePlaceholder;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (localModePage.activeSelf)
        {
            modeIconSelectScript = localModePage.GetComponent<ModeIconSelectScript>();
            imagePlaceholder.sprite = modeImages[modeIconSelectScript.currentIndex];
        }
        else if (onlineModePage.activeSelf)
        {
            modeIconSelectScript = onlineModePage.GetComponent<ModeIconSelectScript>();
            imagePlaceholder.sprite = modeImages[modeIconSelectScript.currentIndex];
        }
        imagePlaceholder.sprite = modeImages[modeIconSelectScript.currentIndex];
    }
}
