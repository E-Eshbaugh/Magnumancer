using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D), typeof(TouchingSpaceDirections))]

public class PlayerController : MonoBehaviour
{
    public Image jumpBar;
    public Sprite jumpBarFull;
    public Sprite jumpBarHalf;
    public Sprite jumpBarEmpty;
    public bool isGrounded;
    public bool isJumping;
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask whatIsGround;
    public float walkSpeed = 5f;
    public float crouchSpeed = 1.5f;
    Vector2 moveInput;
    TouchingSpaceDirections touchingDirecctions;
    public bool doubleJumpAvail = true;
    private bool _isMoving = false;
    

    public bool isMoving
    {
        get { return _isMoving; }
        private set
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
        isGrounded = true;
        isJumping = false;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        touchingDirecctions = GetComponent<TouchingSpaceDirections>();
    }

    void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, whatIsGround);

        if (isGrounded)
        {
            jumpBar.sprite = jumpBarFull;
        }
        else if (!isGrounded && doubleJumpAvail && !diddoubleJump)
        {
            jumpBar.sprite = jumpBarHalf;
        }
        else if (!isGrounded && !doubleJumpAvail && diddoubleJump)
        {
            jumpBar.sprite = jumpBarEmpty;
        }
        
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
        isJumping = true;
        if (context.started && touchingDirecctions.isGrounded)  //check for alive later
        {
            jumpBar.sprite = jumpBarHalf;
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
            jumpBar.sprite = jumpBarEmpty;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpImpulse);
            diddoubleJump = true;
            doubleJumpAvail = false;
        }
    }

    [SerializeField]
    public float rollImpulse = 4f;
    //
    public void OnSlam(InputAction.CallbackContext context)
    {
        if (context.started && doubleJumpAvail)  //check for alive later
        {
            doubleJumpAvail = false;
            animator.SetTrigger("roll");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rollImpulse);
            jumpBar.sprite = jumpBarEmpty;
        }
    }


    public void Interact(InputAction.CallbackContext context)
    //
    {
        if (context.started)  //check for alive later
        {
            animator.SetTrigger("slide");
        }
    }
}
