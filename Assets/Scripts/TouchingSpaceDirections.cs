using UnityEngine;

public class TouchingSpaceDirections : MonoBehaviour
{
    public ContactFilter2D castFilter;
    public float groundDistance = 0.05f;
    CapsuleCollider2D touchingCol;
    Animator animator;
    PlayerController playerController;
    RaycastHit2D[] groundHits = new RaycastHit2D[5];
    private Vector2 wallCheckDirection => playerController._isFacingRight ? Vector2.right : Vector2.left;
    RaycastHit2D[] wallHits = new RaycastHit2D[5];
    RaycastHit2D[] cielingHits = new RaycastHit2D[5];
    public float wallDistance = 0.2f;
    public float cielingDistance = 0.05f;

    [SerializeField]
    private bool _isGrounded;
    public bool isGrounded { 
        get {
            return _isGrounded;
        } private set{
            _isGrounded = value;
            animator.SetBool("isGrounded", _isGrounded);
        } 
    }

    [SerializeField]
    private bool _isOnWall;
    public bool isOnWall { 
        get {
            return _isOnWall;
        } private set{
            _isOnWall = value;
            animator.SetBool("isOnWall", _isOnWall);
        } 
    }
    [SerializeField]
    private bool _isOnCieling;
    public bool isOnCieling { 
        get {
            return _isOnCieling;
        } private set{
            _isOnCieling = value;
        } 
    }

    private void Awake()
    {
        touchingCol = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    void FixedUpdate()
    {
        isGrounded = touchingCol.Cast(Vector2.down, castFilter, groundHits, groundDistance) > 0;
        isOnWall = touchingCol.Cast(wallCheckDirection, castFilter, wallHits, wallDistance) > 0;
        isOnCieling = touchingCol.Cast(Vector2.up, castFilter, cielingHits, cielingDistance) > 0;
    }
}
