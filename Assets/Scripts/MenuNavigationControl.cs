using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuNavigationControl : MonoBehaviour
{
    #region Inspector
    [Header("-- Menu Pages --")]
    public GameObject page1, page2, page3, page4, page5, page6, page7, page8;

    [Header("-- Book Opening Animation --")]
    public GameObject[] framesForward;
    public GameObject[] framesBackward;
    public float frameRate = 0.01f;

    [Header("-- Controllers & Subsystems --")]
    public ControllerConnectScript controllerConnectScript;
    public ModeIconSelectScript[]  modeIconSelectScripts;
    public CharacterSelectController characterSelectController;
    public WeaponSelectControl weaponSelectControl;
    public MapSelectController mapSelectController;
    public MagicManagement magicManagement;
    #endregion

    #region Internal
    private enum MenuPhase { ModeJoin, WizardPick, LoadoutPick, MapPick }
    private MenuPhase phase = MenuPhase.ModeJoin;

    private int currentPicker = 0;
    private bool[] wizardLocked;
    private bool[] loadoutLocked;

    private int currentBookIndex = 0;
    private bool hasStarted = false;

    private int playerCount = 1;
    private int selectedMode = 0;
    private int selectedMap  = 0;

    private Gamepad masterPad;
    #endregion

    #region Singleton
    public static MenuNavigationControl Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }
    #endregion

    private void OnEnable()
    {
        if (controllerConnectScript != null)
            controllerConnectScript.OnFirstPlayerJoined += HandleFirstJoin;
    }

    private void OnDisable()
    {
        if (controllerConnectScript != null)
            controllerConnectScript.OnFirstPlayerJoined -= HandleFirstJoin;
    }

    void Start()
    {
        if (!characterSelectController) characterSelectController = FindFirstObjectByType<CharacterSelectController>();
        if (!mapSelectController)       mapSelectController       = FindFirstObjectByType<MapSelectController>();
        if (!weaponSelectControl)       weaponSelectControl       = FindFirstObjectByType<WeaponSelectControl>();
        if (!magicManagement)           magicManagement           = FindFirstObjectByType<MagicManagement>();

        page3.SetActive(false); page4.SetActive(false);
        page5.SetActive(false); page6.SetActive(false);
        page7.SetActive(false); page8.SetActive(false);

        foreach (var f in framesForward)  if (f) f.SetActive(false);
        foreach (var f in framesBackward) if (f) f.SetActive(false);

        phase = MenuPhase.ModeJoin;
    }

    void HandleFirstJoin(InputDevice dev)
    {
        masterPad = dev as Gamepad;
        DataManager.Instance.SetMasterDevice(dev);

        foreach (var mis in modeIconSelectScripts)
            if (mis) mis.SetPad(masterPad);
    }

    void Update()
    {
        if (hasStarted) return;

        Gamepad pad = ResolveActivePad();
        if (pad == null)
        {
            // Only spam once per phase if needed
            // Debug.LogWarning($"[MenuNav] pad NULL | phase:{phase} picker:{currentPicker} inputs:{DataManager.Instance.Inputs?.Length}");
            return;
        }

        if (pad.aButton.wasPressedThisFrame)
        {
            switch (phase)
            {
                case MenuPhase.ModeJoin:    HandleModeJoinConfirm(); break;
                case MenuPhase.WizardPick:  ConfirmWizard();          break;
                case MenuPhase.LoadoutPick: ConfirmLoadout();         break;
                case MenuPhase.MapPick:     ConfirmMap();             break;
            }
        }
        else if (pad.bButton.wasPressedThisFrame)
        {
            switch (phase)
            {
                case MenuPhase.LoadoutPick: BackToWizard();      break;
                case MenuPhase.WizardPick:  BackToModeJoin();    break;
                case MenuPhase.MapPick:     BackToLastLoadout(); break;
            }
        }
    }

    #region Phase handlers
    private void HandleModeJoinConfirm()
    {
        if (masterPad == null) return;

        // How many players actually joined
        playerCount = Mathf.Clamp(controllerConnectScript.numPlayers, 1, 4);

        // Determine selected mode
        selectedMode = 0;
        if (modeIconSelectScripts != null)
            foreach (var mis in modeIconSelectScripts)
                if (mis && mis.isActiveAndEnabled) { selectedMode = mis.currentIndex; break; }

        // Save device order THEN init arrays (InitPlayers does NOT wipe Inputs)
        DataManager.Instance.SetInputs(controllerConnectScript.JoinedDevices);
        DataManager.Instance.InitPlayers(playerCount);
        DataManager.Instance.SelectedMode = selectedMode;

        wizardLocked  = new bool[playerCount];
        loadoutLocked = new bool[playerCount];
        currentPicker = 0;

        hasStarted = true;
        StartCoroutine(pageForwardAnimation(() =>
        {
            page1.SetActive(false);
            page2.SetActive(false);

            phase = MenuPhase.WizardPick;
            SetWizardSelectActive(true);

            characterSelectController.activePad = DataManager.Instance.GetPad(currentPicker);
            hasStarted = false;
        }));
    }

    private void ConfirmWizard()
    {
        var wiz = characterSelectController.selectedWizard;
        DataManager.Instance.SetWizard(currentPicker, wiz);
        wizardLocked[currentPicker] = true;

        phase = MenuPhase.LoadoutPick;
        hasStarted = true;
        StartCoroutine(pageForwardAnimation(() =>
        {
            SetWizardSelectActive(false);
            SetLoadoutSelectActive(true);

            var pad = DataManager.Instance.GetPad(currentPicker);
            weaponSelectControl.SetActivePlayer(currentPicker, pad);
            magicManagement.SetPlayer(currentPicker);

            hasStarted = false;
        }));
    }

    private void ConfirmLoadout()
    {
        var src  = weaponSelectControl.inventoryData;
        var copy = (WeaponData[])src.Clone();
        DataManager.Instance.SetLoadout(currentPicker, copy);
        loadoutLocked[currentPicker] = true;

        weaponSelectControl.ResetSelection();
        magicManagement.OnSpentChanged(0);

        if (currentPicker < playerCount - 1)
        {
            currentPicker++;
            phase = MenuPhase.WizardPick;

            hasStarted = true;
            StartCoroutine(pageForwardAnimation(() =>
            {
                SetLoadoutSelectActive(false);
                SetWizardSelectActive(true);

                characterSelectController.activePad = DataManager.Instance.GetPad(currentPicker);
                hasStarted = false;
            }));
        }
        else
        {
            phase = MenuPhase.MapPick;
            characterSelectController.activePad = null;

            hasStarted = true;
            StartCoroutine(pageForwardAnimation(() =>
            {
                SetLoadoutSelectActive(false);
                SetMapSelectActive(true);

                mapSelectController.SetActivePad(masterPad);
                hasStarted = false;
            }));
        }
    }

    private void ConfirmMap()
    {
        selectedMap = mapSelectController.currentIndex;
        DataManager.Instance.SelectedMap = selectedMap;

        string sceneName = mapSelectController.SceneNameForIndex(selectedMap);
        SceneManager.LoadScene(sceneName);
    }
    #endregion

    #region Back nav
    private void BackToWizard()
    {
        phase = MenuPhase.WizardPick;
        characterSelectController.activePad = DataManager.Instance.GetPad(currentPicker);

        hasStarted = true;
        StartCoroutine(pageBackwardAnimation(() =>
        {
            SetLoadoutSelectActive(false);
            SetWizardSelectActive(true);
            hasStarted = false;
        }));
    }

    private void BackToModeJoin()
    {
        phase = MenuPhase.ModeJoin;
        currentPicker = 0;
        characterSelectController.activePad = null;

        hasStarted = true;
        StartCoroutine(pageBackwardAnimation(() =>
        {
            SetWizardSelectActive(false);
            page2.SetActive(true);
            page1.SetActive(true);
            hasStarted = false;
        }));
    }

    private void BackToLastLoadout()
    {
        phase = MenuPhase.LoadoutPick;
        currentPicker = playerCount - 1;

        hasStarted = true;
        StartCoroutine(pageBackwardAnimation(() =>
        {
            SetMapSelectActive(false);
            SetLoadoutSelectActive(true);
            hasStarted = false;
        }));
    }
    #endregion

    #region UI toggles
    private void SetWizardSelectActive(bool on){ page3.SetActive(on); page4.SetActive(on); }
    private void SetLoadoutSelectActive(bool on){ page5.SetActive(on); page6.SetActive(on); }
    private void SetMapSelectActive(bool on){ page7.SetActive(on); page8.SetActive(on); }
    #endregion

    #region Anim
    System.Collections.IEnumerator pageForwardAnimation(Action onComplete)
    {
        if (framesForward == null || framesForward.Length == 0){ onComplete?.Invoke(); yield break; }

        framesForward[currentBookIndex].SetActive(true);
        while (currentBookIndex < framesForward.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            framesForward[currentBookIndex++].SetActive(false);
            framesForward[currentBookIndex].SetActive(true);
        }
        ResetFrames(framesForward);
        currentBookIndex = 0;
        onComplete?.Invoke();
    }

    System.Collections.IEnumerator pageBackwardAnimation(Action onComplete)
    {
        if (framesBackward == null || framesBackward.Length == 0){ onComplete?.Invoke(); yield break; }

        framesBackward[currentBookIndex].SetActive(true);
        while (currentBookIndex < framesBackward.Length - 1)
        {
            yield return new WaitForSeconds(frameRate);
            framesBackward[currentBookIndex++].SetActive(false);
            framesBackward[currentBookIndex].SetActive(true);
        }
        ResetFrames(framesBackward);
        currentBookIndex = 0;
        onComplete?.Invoke();
    }

    void ResetFrames(GameObject[] frames){ foreach (var f in frames) if (f) f.SetActive(false); }
    #endregion

    #region Pads
    private Gamepad ResolveActivePad()
    {
        if (phase == MenuPhase.ModeJoin || phase == MenuPhase.MapPick)
            return masterPad ?? DataManager.Instance.GetMasterPad();
        return DataManager.Instance.GetPad(currentPicker);
    }
    #endregion
}
