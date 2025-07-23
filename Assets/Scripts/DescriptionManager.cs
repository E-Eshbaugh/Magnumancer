using UnityEngine;
using UnityEngine.UI;

public class DescriptionManager : MonoBehaviour
{
    public Text decriptionText;
    public ModeIconSelectScript modeIconSelectScript;
    public GameObject onlineModePage;
    public GameObject localModePage;
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
        }
        else if (onlineModePage.activeSelf)
        {
            modeIconSelectScript = onlineModePage.GetComponent<ModeIconSelectScript>();
        }

        switch (modeIconSelectScript.currentIndex)
        {
            case 0:
                decriptionText.text = "The leyline circle cracks and hisses as four magi stride in alone - no oath, no ally, only will. Friends are few, spells are many, and the last soul standing drinks the fading light.";
                break;
            case 1:
                decriptionText.text = "Two magi bound by a silent pact face two sworn opposites. Hold formation like mirrored sigils and reap their lives in tandem to bring glory to your pact.";
                break;
            case 2:
                decriptionText.text = "At the jagged mouth of a Dark Rift, goblin tides claw their way up from the black. Hold the front lines with your coven, drafting Incantations between breaths, until either the rift stills...or you do.";
                break;
            default:
                decriptionText.text = "At the jagged mouth of a Dark Rift, goblin tides claw their way up from the black. Hold the front lines with your coven, drafting Incantations between breaths, until either the rift stills - or you do.";
                break;
        }
    }
}
