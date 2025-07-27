using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct PlayerInputRef
{
    public int  deviceId;   // InputDevice.deviceId
    public bool isKeyboard; // in case someone joined on KB
}

public class DataManager : MonoBehaviour
{
    public int MasterDeviceId = -1;
    public void SetMasterDevice(InputDevice dev) => MasterDeviceId = dev?.deviceId ?? -1;
    public Gamepad GetMasterPad() => MasterDeviceId >= 0 ? InputSystem.GetDeviceById(MasterDeviceId) as Gamepad : null;

    public static DataManager Instance { get; private set; }

    public int NumPlayers { get; private set; } = 1;
    public int SelectedMode { get; set; }
    public int SelectedMap { get; set; }

    public WizardData[] Wizards { get; private set; }
    public WeaponData[][] Loadouts { get; private set; }

    public PlayerInputRef[] Inputs { get; private set; }   // <— NEW

    void Awake()
    {
        Debug.Log($"[DataManager] Awake in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DataManager] New instance created.");
        }
        else
        {
            Destroy(gameObject);
            Debug.Log("[DataManager] Duplicate destroyed.");
        }
    }


    public void InitPlayers(int count)
    {
        NumPlayers = Mathf.Clamp(count, 1, 4);

        if (Wizards == null || Wizards.Length  != NumPlayers)
            Wizards  = new WizardData[NumPlayers];

        if (Loadouts == null || Loadouts.Length != NumPlayers)
            Loadouts = new WeaponData[NumPlayers][];

        if (Inputs != null && Inputs.Length != NumPlayers)
        {
            var newArr = new PlayerInputRef[NumPlayers];
            for (int i = 0; i < Mathf.Min(Inputs.Length, NumPlayers); i++)
                newArr[i] = Inputs[i];
            Inputs = newArr;
        }
    }



    public void SetInputs(IReadOnlyList<InputDevice> devices)
    {
        if (devices == null) return;
        if (Inputs == null || Inputs.Length != devices.Count)
            Inputs = new PlayerInputRef[devices.Count];

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            Inputs[i] = new PlayerInputRef
            {
                deviceId = d.deviceId,
                isKeyboard = d is Keyboard
            };
        }
    }

    public Gamepad GetPad(int index)
    {
        if (Inputs == null || index < 0 || index >= Inputs.Length) return null;
        return InputSystem.GetDeviceById(Inputs[index].deviceId) as Gamepad;
    }

    public Keyboard GetKeyboard(int index)
    {
        if (Inputs == null || index < 0 || index >= Inputs.Length) return null;
        return InputSystem.GetDeviceById(Inputs[index].deviceId) as Keyboard;
    }

    public void SetWizard(int slot, WizardData data)
    {
        if (slot < 0 || slot >= NumPlayers) return;
        Wizards[slot] = data;
    }

    public WizardData GetWizard(int slot) =>
        (slot >= 0 && slot < NumPlayers) ? Wizards[slot] : null;

    public void SetLoadout(int slot, WeaponData[] data)
    {
        if (slot < 0 || slot >= NumPlayers) return;
        Loadouts[slot] = data != null ? (WeaponData[])data.Clone() : null;
    }

    public WeaponData[] GetLoadout(int slot) =>
        (slot >= 0 && slot < NumPlayers) ? Loadouts[slot] : null;
        
    public InputDevice GetDevice(int index)
    {
        if (Inputs == null || index < 0 || index >= Inputs.Length) return null;
        return InputSystem.GetDeviceById(Inputs[index].deviceId);
    }

}
