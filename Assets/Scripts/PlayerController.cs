using System;
using System.Collections;
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
    public Image dashBar;
    public Sprite dashFull;
    public Sprite dashEmpty;
    public Sprite dash1;
    public Sprite dash2;
    public Sprite dash3;
    public Sprite dash4;
    public bool isGrounded;
    public bool isJumping;
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask whatIsGround;
    public float walkSpeed = 10f;
    public bool dash = true;
    public bool isDashing = false;
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

    public bool isCrouched
    {
        get { return _isCrouched; }
        private set
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
                return walkSpeed;

            }
            else
            {
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
    public float dashImpulse = 20f;

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

        if (isGrounded && isJumping)
        {
            isJumping = false;
        }

        if (isGrounded)
        {
            jumpBar.sprite = jumpBarFull;
        }
        else if (!isGrounded && doubleJumpAvail && !diddoubleJump && isJumping)
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
        if (!isDashing)
        {
            rb.linearVelocity = new Vector2(moveInput.x * CurrentMoveSpeed, rb.linearVelocityY);
            animator.SetFloat("yVel", rb.linearVelocityY);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        isMoving = moveInput != Vector2.zero;
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.started && dash)  //check for alive later
        {
            //animator.SetTrigger("dash");
            StartCoroutine(DashCooldown());
            rb.AddForce(new Vector2(dashImpulse * (isFacingRight() ? 1 : -1), 0), ForceMode2D.Impulse);
            dash = false;
            isDashing = true;
            dashBar.sprite = dashEmpty;
            StartCoroutine(DashDuration());
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

    private IEnumerator DashCooldown()
    {
        yield return new WaitForSeconds(0.5f);
        dashBar.sprite = dash1;

        yield return new WaitForSeconds(0.5f);
        dashBar.sprite = dash2;

        yield return new WaitForSeconds(0.5f);
        dashBar.sprite = dash3;

        yield return new WaitForSeconds(0.5f);
        dashBar.sprite = dash4;

        yield return new WaitForSeconds(0.5f);
        dashBar.sprite = dashFull;

        dash = true;
    }
    
    private IEnumerator DashDuration()
    {
        yield return new WaitForSeconds(0.2f);
        isDashing = false;
    }
}
