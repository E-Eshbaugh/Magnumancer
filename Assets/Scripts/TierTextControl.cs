using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TierTextControl : MonoBehaviour
{
    public Text tierText;
    private String[] tierNames = { "Initiate", "Ascended", "Archon" };
    private int currentTier = 0;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Gamepad.current.rightShoulder.wasPressedThisFrame)
        {
            currentTier = currentTier + 1;

            if (currentTier >= tierNames.Length)
            {
                currentTier = 0;
            }
        }
        else if (Gamepad.current.leftShoulder.wasPressedThisFrame)
        {
            currentTier = currentTier - 1;

            if (currentTier < 0)
            {
                currentTier = tierNames.Length - 1;
            }
        }

        tierText.text = tierNames[currentTier];
    }
}
