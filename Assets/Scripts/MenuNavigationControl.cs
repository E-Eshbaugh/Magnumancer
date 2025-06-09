using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuNavigationControl : MonoBehaviour
{
    [Header("-- Menu Pages --")]
    public GameObject page1;
    public GameObject page2;
    public GameObject page3;
    public GameObject page4;
    public GameObject page5;
    public GameObject page6;

    [Header("-- Book Opening Animation --")]
    public GameObject[] framesForward;
    public GameObject[] framesBackward;
    public float frameRate = 0.01f;
    private int currentBookIndex = 0;
    private bool hasStarted = false;
    public WizardData selectedWizard;

    [Header("-- Character Select --")]
    public CharacterSelectController characterSelectController;
    public static MenuNavigationControl Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Grab reference if not set
        if (characterSelectController == null) characterSelectController = FindFirstObjectByType<CharacterSelectController>();

        page3.SetActive(false);
        page4.SetActive(false);
        page5.SetActive(false);
        page6.SetActive(false);

        foreach (var f in framesForward) f.SetActive(false);
        foreach (var f in framesBackward) f.SetActive(false);
    }

    void Update()
    {
        if (hasStarted) return;

        if (Gamepad.current.aButton.wasPressedThisFrame)
        {
            if (page1.activeSelf)
            {
                // Proceed to character select -> loadout
                hasStarted = true;
                StartCoroutine(pageForwardAnimation(() =>
                {
                    selectedWizard = characterSelectController.selectedWizard;
                    page1.SetActive(false);
                    page2.SetActive(false);
                    page3.SetActive(true);
                    page4.SetActive(true);
                    hasStarted = false;
                }));
            }
            else if (page3.activeSelf)
            {
                // Proceed to loadout -> map select
                hasStarted = true;
                StartCoroutine(pageForwardAnimation(() =>
                {
                    page3.SetActive(false);
                    page4.SetActive(false);
                    page5.SetActive(true);
                    page6.SetActive(true);
                    hasStarted = false;
                }));
            }
            else if (page5.activeSelf)
            {
                SceneManager.LoadScene("OldWoods");
            }
        }
        else if (Gamepad.current.bButton.wasPressedThisFrame)
        {
            if (page5.activeSelf)
            {
                hasStarted = true;
                StartCoroutine(pageBackwardAnimation(() =>
                {
                    page6.SetActive(false);
                    page5.SetActive(false);
                    page4.SetActive(true);
                    page3.SetActive(true);
                    hasStarted = false;
                }));
            }
            else if (page3.activeSelf)
            {
                hasStarted = true;
                StartCoroutine(pageBackwardAnimation(() =>
                {
                    page3.SetActive(false);
                    page4.SetActive(false);
                    page2.SetActive(true);
                    page1.SetActive(true);
                    hasStarted = false;
                }));
            }
            else if (page1.activeSelf)
            {
                Debug.Log("Book is closed. Exit menu?");
                // Optional: implement quit logic
            }
        }
    }

    System.Collections.IEnumerator pageForwardAnimation(System.Action onComplete)
    {
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

    System.Collections.IEnumerator pageBackwardAnimation(System.Action onComplete)
    {
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

    void ResetFrames(GameObject[] frames)
    {
        foreach (var f in frames) f.SetActive(false);
    }
}
