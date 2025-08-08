using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CurPlayerTxtController : MonoBehaviour
{
    [Header("References")]
    public Text text;
    public ControllerConnectScript controllerConnect;

    private InputDevice currentDevice;

    void Update()
    {
        if (controllerConnect == null || currentDevice == null)
        {
            text.text = "";
            return;
        }

        int index = controllerConnect.GetPlayerIndex(currentDevice);
        if (index >= 0)
            text.text = $"{index + 1}";
        else
            text.text = "";
    }

    // Call this from your menu script to set the active player
    public void SetCurrentDevice(InputDevice device)
    {
        currentDevice = device;
    }
}
