using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GunAmmo : MonoBehaviour
{
    [Header("- Firing Mechanics -")]
    public GameObject bulletPrefab;
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
        //fire point setup =====================================================
        Vector2 gunSize = gunRenderer.bounds.size;
        firePoint.localPosition = new Vector3(gunSize.x, 0f, 0f);

        // Gun Setup ====================================================
        gunName = gunRenderer.sprite.name;
        //mini gun
        if (gunName == "1_50")
        {
            maxAmmoCount = 150;
            ammoCount = maxAmmoCount;
            damage = 10;
            fireRate = 0.075f;
            fireType = "auto";
            weight = 50;
        }
        //heavy sniper
        else if (gunName == "1_49")
        {
            maxAmmoCount = 10;
            ammoCount = maxAmmoCount;
            damage = 50;
            fireRate = 1f;
            fireType = "semi";
            weight = 30;
        }
        //ak
        else if (gunName == "1_56")
        {
            maxAmmoCount = 25;
            ammoCount = maxAmmoCount;
            damage = 30;
            fireRate = 0.15f;
            fireType = "auto";
            weight = 20;
        }
        //smg
        else if (gunName == "1_62")
        {
            maxAmmoCount = 50;
            ammoCount = maxAmmoCount;
            damage = 20;
            fireRate = 0.1f;
            fireType = "auto";
            weight = 10;
        }
        // ================================================

        if (animator == null)
            animator = fireSprite.GetComponent<Animator>();
        
        if(newController != null) {
            animator.runtimeAnimatorController = newController;
        }
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

}
