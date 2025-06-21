using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Animator animator;
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float verticalVelocity = 0f;

    private Vector2 moveInput;
    private Vector3 lastDirection = Vector3.forward;
    private CharacterController controller;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        // Filter small noisy inputs
        Vector2 filteredInput = (moveInput.magnitude < 0.1f) ? Vector2.zero : moveInput;

        // Isometric movement direction
        Vector3 inputDir = new Vector3(filteredInput.x, 0f, filteredInput.y).normalized;
        Vector3 isoDir = IsoRotation * inputDir;

        // Save last known movement direction (if valid)
        if (isoDir.sqrMagnitude > 0.01f)
            lastDirection = isoDir;

        Vector3 horizontalMove = lastDirection * moveSpeed * filteredInput.magnitude;

        // Gravity
        if (controller.isGrounded)
            verticalVelocity = -0.5f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // Final move vector
        Vector3 finalMove = new Vector3(horizontalMove.x, verticalVelocity, horizontalMove.z);
        controller.Move(finalMove * Time.deltaTime);

        // Rotate to face last known movement direction
        if (filteredInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        // Animate
        animator.SetFloat("Speed", filteredInput.magnitude);
    }
}

