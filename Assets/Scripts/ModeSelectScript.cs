using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ModeSelectScript : MonoBehaviour
{
    public Text modeText;
    public GameObject controllerconnectPanelLocal;
    public GameObject controllerconnectPanelOnline;
    [Header("Pages")]
    public GameObject onlineModePage;
    public GameObject localModePage;

    [Header("Input")]
    public Gamepad gamepad;
    [SerializeField] float toggleCooldown = 0.2f;

    float _nextToggleTime;

    void Awake()
    {
        if (gamepad == null) gamepad = Gamepad.current;
    }

    void Start()
    {
        ShowLocal(); // default
    }

    void Update()
    {
        if (gamepad == null) return;

        if (Time.unscaledTime < _nextToggleTime) return;

        // Right shoulder -> online, Left -> local (or just toggle on either)
        if (gamepad.rightShoulder.wasPressedThisFrame)
        {
            TogglePages();
        }
        else if (gamepad.leftShoulder.wasPressedThisFrame)
        {
            TogglePages();
        }

        switch (onlineModePage.activeSelf)
        {
            case true:
                modeText.text = "Online";
                break;
            case false:
                modeText.text = "Local";
                break;
        }
    }

    void TogglePages()
    {
        bool goOnline = !onlineModePage.activeSelf; // if offline now, go online
        if (goOnline) ShowOnline(); else ShowLocal();
        _nextToggleTime = Time.unscaledTime + toggleCooldown;
    }

    void ShowOnline()
    {
        onlineModePage.SetActive(true);
        localModePage.SetActive(false);
        controllerconnectPanelLocal.SetActive(false);
        controllerconnectPanelOnline.SetActive(true);
    }

    void ShowLocal()
    {
        onlineModePage.SetActive(false);
        localModePage.SetActive(true);
        controllerconnectPanelLocal.SetActive(true);
        controllerconnectPanelOnline.SetActive(false);
    }
}
