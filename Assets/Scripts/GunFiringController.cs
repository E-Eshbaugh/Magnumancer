using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GunAmmo : MonoBehaviour
{
    [Header("- Firing Mechanics -")]
    public GameObject bulletPrefab;
    public PlayerController playerController;
    public Transform firePoint;
    public GameObject fireSprite;
    private float nextFireTime = 0f;
    public float fireRate;
    public String fireType;
    public Animator animator;
    public RuntimeAnimatorController newController;
    private bool _isShooting = false;
    public bool shooting = false;
    public bool isShooting
    {
        get { return _isShooting; }
        private set
        {
            _isShooting = value;
            animator.SetBool("isShooting", _isShooting);
        }
    }

    [Header("- Gun Ammo -")]
    public Image ammoBar;
    public Sprite ammoBarFull;
    public Sprite ammoBar80;
    public Sprite ammoBar60;
    public Sprite ammoBar40;
    public Sprite ammoBar20;
    public Sprite ammoBarEmpty;
    [Header("- Gun -")]
    public SpriteRenderer gunRenderer;
    private String gunName;
    public int ammoCount;
    public int maxAmmoCount;
    public int damage;
    public int weight;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        updateGun();
    }

    // Update is called once per frame
    void Update()
    {
        float currentTime = Time.time;
        //firing
        if (fireType == "semi")
        {
            if (Gamepad.current.rightTrigger.wasPressedThisFrame && currentTime >= nextFireTime)
            {
                Fire();
                nextFireTime = currentTime + fireRate;
            }
            else
            {
                isShooting = false;
                shooting = false;
            }
        }
        else if (fireType == "auto")
        {
            if (Gamepad.current.rightTrigger.isPressed && Time.time >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.time + fireRate;
            }
            else
            {
                isShooting = false;
                shooting = false;
            }
        }

        //ammo count
        if (ammoCount >= maxAmmoCount * 0.8)
        {
            ammoBar.sprite = ammoBarFull;
        }
        else if (ammoCount >= maxAmmoCount * 0.6)
        {
            ammoBar.sprite = ammoBar80;
        }
        else if (ammoCount >= maxAmmoCount * 0.4)
        {
            ammoBar.sprite = ammoBar60;
        }
        else if (ammoCount >= maxAmmoCount * 0.2)
        {
            ammoBar.sprite = ammoBar40;
        }
        else if (ammoCount > 0)
        {
            ammoBar.sprite = ammoBar20;
        }
        else
        {
            ammoBar.sprite = ammoBarEmpty;
        }
    }

    void Fire()
    {
        if (ammoCount <= 0)
        {
            return;
        }

        isShooting = true;
        shooting = true;
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        ammoCount--;
    }

    public void updateGun()
    {
        

    }
}
