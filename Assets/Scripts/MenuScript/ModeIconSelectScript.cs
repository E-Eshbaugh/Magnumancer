using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
// using TMPro;  // <- if you move to TextMeshPro

public class ModeIconSelectScript : MonoBehaviour
{
    [Header("Icons & Text")]
    public Image[] modeIcons;          // DM, TDM, Waves (in order)
    public Sprite[] ogModeIcons;       // black
    public Sprite[] altModeIcons;      // glowing blue
    public Text[] modeTexts;           // matching order to icons
                                       // public TMP_Text[] modeTMPTexts;  // if using TMP instead
    public Text playerNumText;

    [Header("Colors")]
    public Color normalTextColor = Color.black;
    public Color selectedTextColor = new Color32(0x00, 0xE5, 0xFF, 0xFF); // #00E5FF

    [Header("Input")]
    public Gamepad gamepad;
    public void SetPad(Gamepad pad) => gamepad = pad;
    [SerializeField] float stickThreshold = 0.6f;

    [Header("Behaviour")]
    public int currentIndex = 0;
    public int maxModes = 3;           // auto-trimmed by tag if needed
    [SerializeField] float repeatDelay = 0.25f;

    float _nextMoveTime;
    bool prevLeftHeld, prevRightHeld;

    void Awake()
    {
        if (CompareTag("onlineModes")) maxModes = 2;
        else if (CompareTag("localModes")) maxModes = 3;
        else maxModes = Mathf.Min(modeIcons.Length, ogModeIcons.Length, altModeIcons.Length, modeTexts.Length);

        for (int i = 0; i < maxModes; i++)
            Debug.Log($"{name} slot {i}: icon={modeIcons[i]?.name} text={modeTexts[i]?.name}");

        ApplyVisuals();
    }

    void OnEnable()
    {
        currentIndex = Mathf.Clamp(currentIndex, 0, maxModes - 1);
        ApplyVisuals();
    }


    void Update()
    {
        if (gamepad == null) return;

        float x = gamepad.leftStick.ReadValue().x;
        bool rightHeld = x > stickThreshold;
        bool leftHeld = x < -stickThreshold;

        float now = Time.unscaledTime;
        if (now >= _nextMoveTime)
        {
            if (rightHeld && !prevRightHeld) { Move(+1); _nextMoveTime = now + repeatDelay; }
            if (leftHeld && !prevLeftHeld) { Move(-1); _nextMoveTime = now + repeatDelay; }
        }

        prevRightHeld = rightHeld;
        prevLeftHeld = leftHeld;

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            string sel = GetModeName(currentIndex);
            // Do something with sel
            // Debug.Log("Selected: " + sel);
        }

        if (currentIndex == 2)
        {
            playerNumText.text = "1-4 Players";
        }
        else
        {
            playerNumText.text = "2-4 Players";
        }
    }

    void Move(int dir)
    {
        currentIndex = (currentIndex + dir + maxModes) % maxModes;
        ApplyVisuals();
    }
    void ApplyVisuals()
    {
        int slots = Mathf.Min(
            maxModes,
            modeIcons.Length,
            ogModeIcons.Length,
            altModeIcons.Length,
            modeTexts.Length
        );

        for (int i = 0; i < slots; i++)
        {
            bool isSelected = i == currentIndex;

            if (modeIcons[i])
                modeIcons[i].sprite = isSelected ? altModeIcons[i] : ogModeIcons[i];

            if (modeTexts[i])
            {
                // Never deactivate, just recolor
                // modeTexts[i].gameObject.SetActive(true);
                if (modeTexts[i].canvasRenderer.GetAlpha() < 1f)
                    modeTexts[i].canvasRenderer.SetAlpha(1f);

                modeTexts[i].color = isSelected ? selectedTextColor : normalTextColor;
                // Ensure on top if needed
                // modeTexts[i].transform.SetAsLastSibling();
            }

            Debug.Log($"[{name}] i={i} sel={isSelected} col={modeTexts[i].color} alpha={modeTexts[i].canvasRenderer.GetAlpha()} active={modeTexts[i].gameObject.activeInHierarchy}");

        }
    }


    string GetModeName(int i) => i switch
    {
        0 => "DM",
        1 => "TDM",
        2 => "Waves",
        _ => "Unknown"
    };
}
