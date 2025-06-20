using UnityEngine;
using UnityEngine.InputSystem;

public class GunOrbitController : MonoBehaviour
{
    private Vector2 moveInput;
    private CharacterController controller;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);
    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    //create a method that orbits the gun around the player based on right stick input
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 isoInput = IsoRotation * input;

        

        // Rotate the gun based on input
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 direction = IsoRotation * new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

    }
    
    public void OnLook(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}
