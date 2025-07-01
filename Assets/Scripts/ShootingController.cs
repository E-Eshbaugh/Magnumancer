using UnityEngine;
using UnityEngine.InputSystem;

public class ShootingController : MonoBehaviour
{
    [SerializeField]
    public bool shooting = false;
    public Transform FirePoint;
    public GameObject Bullet;
    public Animator animator;
    public RuntimeAnimatorController newController;

    private bool _isShooting = false;
    public bool isShooting { get{return _isShooting;} private set 
        {
            _isShooting = value;
            animator.SetBool("isShooting", _isShooting);
        } 
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if(newController != null) {
            animator.runtimeAnimatorController = newController;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Gamepad.current.rightTrigger.isPressed)
        {
            isShooting = true;
            shooting = true;
            
            Shoot();
        } else{
            isShooting = false;
            shooting = false;
        }
    }

    void Shoot() 
    {
        Instantiate(Bullet, FirePoint.position, FirePoint.rotation);
    }
}
