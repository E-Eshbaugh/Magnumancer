using UnityEngine;
using UnityEngine.InputSystem;

public class SpellMenuControl : MonoBehaviour
{
    public SpriteRenderer gunRenderer;
    public SpriteRenderer gunUp;
    public SpriteRenderer gunDown;
    public SpriteRenderer gunLeft;
    public SpriteRenderer gunRight;
    public GunAmmo firingController;
    void Start()
    {
        firingController = FindFirstObjectByType<GunAmmo>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isActiveAndEnabled)
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame)
            {
                gunRenderer.sprite = gunUp.sprite;
                firingController.updateGun();
            }
            else if (Gamepad.current.dpad.down.wasPressedThisFrame)
            {
                gunRenderer.sprite = gunDown.sprite;
                firingController.updateGun();
            }
            else if (Gamepad.current.dpad.left.wasPressedThisFrame)
            {
                gunRenderer.sprite = gunLeft.sprite;
                firingController.updateGun();
            }
            else if (Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                gunRenderer.sprite = gunRight.sprite;
                firingController.updateGun();

            }
        }
    }



}
