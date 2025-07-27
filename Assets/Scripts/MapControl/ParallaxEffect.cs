using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ParallaxEffect : MonoBehaviour
{
    public Camera cam;
    public Transform followTarget;
    UnityEngine.Vector2 startingPosition;
    float startingZ;
    UnityEngine.Vector2 camMoveSinceStart => (UnityEngine.Vector2)cam.transform.position - startingPosition;
    float distanceFromTarget => transform.position.z - followTarget.position.z;
    float clippingPlane => (cam.transform.position.z + (distanceFromTarget > 0 ? cam.farClipPlane : cam.nearClipPlane));
    float parallaxFactor => Mathf.Abs(distanceFromTarget) / clippingPlane;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startingPosition = transform.position;
        startingZ = transform.position.z;
    }

    // Update is called once per frame
    void Update()
    {
        UnityEngine.Vector2 newPosition = startingPosition + camMoveSinceStart * parallaxFactor;
        transform.position = new UnityEngine.Vector3(newPosition.x, newPosition.y, startingZ);
    }
}
