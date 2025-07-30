using System.Collections;
using Magnumancer.Abilities;
using UnityEngine;

public class WaterClone : MonoBehaviour, IActiveAbility
{
    public GameObject clonePrefab; // Prefab to instantiate as a clone
    public float cloneDuration = 5f; // Duration before the clone is destroyed
    public Vector3 cloneOffset = new Vector3(1, 0, 0);
    public GameObject smoke;
    public void Activate(GameObject caster)
    {
        if (caster == null) return;
        // 🔵 Spawn mist
        if (smoke != null)
            Instantiate(smoke, caster.transform.position, Quaternion.identity);
        GameObject clone = Instantiate(clonePrefab, caster.transform.position + cloneOffset, caster.transform.rotation);
        clone.name = $"{caster.name}_Clone";

        PlayerAppearance cloneAppearance = clone.GetComponent<PlayerAppearance>();
        if (cloneAppearance != null)
        {
            var appearance = clone.GetComponentInChildren<PlayerAppearance>();
            if (appearance != null)
            {
                appearance.Setup(caster.GetComponent<PlayerMovement3D>().wizard);
                appearance.makeTransparent = true;
            }
        }

        var cloneMovement = clone.GetComponent<CloneMovement>();
        if (cloneMovement != null)
        {
            cloneMovement.gamepad = caster.GetComponent<PlayerMovement3D>().gamepad;
            cloneMovement.moveSpeed = caster.GetComponent<PlayerMovement3D>().currentMoveSpeed;
        }

        var orbit = clone.GetComponentInChildren<CloneGunOrbit>();
        if (orbit != null)
        {
            orbit.Setup(caster.GetComponent<PlayerMovement3D>().gamepad, clone.transform);
        }

        StartCoroutine(DestroyCloneAfterDelay(clone, cloneDuration));
    }


    private IEnumerator DestroyCloneAfterDelay(GameObject clone, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clone != null)
        {
            if (smoke != null)
            {
                Instantiate(smoke, clone.transform.position, Quaternion.identity);
            }
            
            Destroy(clone);
        }
    }

}