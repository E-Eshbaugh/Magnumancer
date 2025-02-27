using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(TouchingSpaceDirections))]

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    Vector2 moveInput;
    TouchingSpaceDirections touchingDirecctions;
    public bool doubleJumpAvail = true;

    private bool _isMoving = false;
    public bool isMoving { get{return _isMoving;} private set 
        {
            _isMoving = value;
            animator.SetBool("isMoving", _isMoving);
        } 
    }
    private bool _isCrouched = false;

    public bool isCrouched { get{return _isCrouched;} private set 
        {
            _isCrouched = value;
            animator.SetBool("isCrouch", _isCrouched);
        } 
    }

    public float CurrentMoveSpeed 
    {
        get
        {
            if (isMoving && !touchingDirecctions.isOnWall)
            {
                if (isCrouched)
                {
                    return crouchSpeed;
                } else {
                    return walkSpeed;
                }
            } else {
                return 0f;
            }
        }
    }
    
    public bool _isFacingRight = true;

    public bool isFacingRight()
    {
        return _isFacingRight;
    }

    Rigidbody2D rb;
    Animator animator;
    [SerializeField]
     public float jumpImpulse = 20f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        touchingDirecctions = GetComponent<TouchingSpaceDirections>();
    }

    void Update()
    {
        if (touchingDirecctions.isGrounded)
        {
            doubleJumpAvail = true;
            diddoubleJump = false;
        }

        float horizontalInput = Input.GetAxis("Horizontal");
        
        // Flip direction when input changes
        if (horizontalInput > 0 && !_isFacingRight)
            Flip();
        else if (horizontalInput < 0 && _isFacingRight)
            Flip();
    }

    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        transform.Rotate(0f, 180f, 0f);
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(moveInput.x * CurrentMoveSpeed, rb.linearVelocityY);
        animator.SetFloat("yVel", rb.linearVelocityY);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        isMoving = moveInput != Vector2.zero;
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            if (isCrouched)
            {
                isCrouched = false;
            } else {
                isCrouched = true;
            }
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && touchingDirecctions.isGrounded)  //check for alive later
        {
            animator.SetTrigger("jump");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpImpulse);
        }
    }

    [SerializeField]
    public bool diddoubleJump = false;
    public void OnDoubleJump(InputAction.CallbackContext context)
    {
        if (context.started && !touchingDirecctions.isGrounded && doubleJumpAvail)  //check for alive later
        {
            animator.SetTrigger("doubleJump");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpImpulse);
            diddoubleJump = true;
            doubleJumpAvail = false;
        }
    }

    [SerializeField]
    public float rollImpulse = 4f;
    public void OnRoll(InputAction.CallbackContext context)
    {
        if (context.started && touchingDirecctions.isGrounded)  //check for alive later
        {
            animator.SetTrigger("roll");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rollImpulse);
        }
    }
    
    [SerializeField]
    public float slideImpulse = 4f;
    public void OnSlide(InputAction.CallbackContext context)
    {
        if (context.started && touchingDirecctions.isGrounded)  //check for alive later
        {
            animator.SetTrigger("slide");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, slideImpulse);
        }
    }
}
