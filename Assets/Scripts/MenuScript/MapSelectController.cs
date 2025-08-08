using UnityEngine;
using UnityEngine.InputSystem;

public class MapSelectController : MonoBehaviour
{
    [Header("-- Grid --")]
    public Transform[] mapTiles;
    public int columns = 4;
    public Transform selectorIcon;

    [Header("-- Input --")]
    public float inputCooldown = 0.2f;
    public float stickDeadzone = 0.5f;

    private float inputTimer;
    private Gamepad pad;

    public int currentIndex { get; private set; }

    void OnEnable()
    {
        // keep selector on correct tile when page opens
        if (selectorIcon && mapTiles.Length > 0)
            selectorIcon.position = mapTiles[currentIndex].position;
        inputTimer = 0f;
    }

    void Update()
    {
        if (pad == null) return;                      // <-- if this is null, you forgot SetActivePad

        inputTimer += Time.deltaTime;

        if (inputTimer >= inputCooldown)
        {
            Vector2 rs = pad.rightStick.ReadValue();
            Vector2 dp = pad.dpad.ReadValue();        // let dpad work too

            if (Mathf.Abs(rs.x) > stickDeadzone || Mathf.Abs(dp.x) > 0.5f)
            {
                TryMove((rs.x > 0 || dp.x > 0.5f) ? 1 : -1);
                inputTimer = 0f;
            }
            else if (Mathf.Abs(rs.y) > stickDeadzone || Mathf.Abs(dp.y) > 0.5f)
            {
                TryMove((rs.y > 0 || dp.y > 0.5f) ? -columns : columns);
                inputTimer = 0f;
            }
        }

        if (selectorIcon && mapTiles.Length > 0)
            selectorIcon.position = mapTiles[currentIndex].position;
    }

    void TryMove(int delta)
    {
        int newIndex = currentIndex + delta;
        if (newIndex < 0 || newIndex >= mapTiles.Length) return;

        // horizontal row clamp
        if (Mathf.Abs(delta) == 1)
        {
            int oldRow = currentIndex / columns;
            int newRow = newIndex / columns;
            if (oldRow != newRow) return;
        }

        currentIndex = newIndex;
        // (optional SFX/animation hook here)
    }

    // --- Called by MenuNavigationControl ---
    public void SetActivePad(Gamepad activePad)
    {
        pad = activePad;
    }

    public void ResetInput()
    {
        inputTimer = 0f;
    }

    public string SceneNameForIndex(int idx)
    {
        switch (idx)
        {
            case 0: return "Oldwoods3D";
            case 1: return "Stormspire";
            case 2: return "FungalHollow";
            case 3: return "Riftforge";
            case 4: return "CinderCrucibleZombies";
            case 5: return "DrownedSanctum";
            case 6: return "BlackOsuary";
            case 7: return "Frostgrave";
            default: return "Oldwoods3D";
        }
    }
}
