using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningStrike : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;

    [Header("Strike Settings")]
    public int segments = 12;
    public float jaggedness = 1.5f;
    public float strikeDuration = 0.15f;
    public float strikeInterval = 0.5f;

    private LineRenderer line;
    private float timer;
    private bool isStriking = false;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 0;
        timer = strikeInterval;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (!isStriking && timer <= 0f)
        {
            StartCoroutine(Strike());
            timer = strikeInterval;
        }
    }

    IEnumerator Strike()
    {
        isStriking = true;

        // Move & rotate the object so Z+ points from start to end
        transform.position = startPoint.position;
        Vector3 direction = (endPoint.position - startPoint.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);

        // Generate local-space jagged points along Z axis
        List<Vector3> points = new();

        float length = Vector3.Distance(startPoint.position, endPoint.position);
        Vector3 localRight = transform.right;
        Vector3 localUp = transform.up;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (segments - 1f);
            float zPos = t * length;

            Vector3 point = new Vector3(0f, 0f, zPos);

            // Add randomized offset in both horizontal and vertical directions (perpendicular to forward)
            Vector3 offset = (localRight * Random.Range(-jaggedness, jaggedness) +
                            localUp * Random.Range(-jaggedness, jaggedness)) *
                            Mathf.Sin(t * Mathf.PI);

            // Convert to local space
            Vector3 final = point + transform.InverseTransformDirection(offset);
            points.Add(final);
        }


        line.positionCount = segments;
        line.SetPositions(points.ToArray());
        line.enabled = true;

        yield return new WaitForSeconds(strikeDuration);

        line.enabled = false;
        isStriking = false;
    }
}
