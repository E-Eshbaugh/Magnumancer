using UnityEngine;

public class CrystalHealth : MonoBehaviour
{
    public int health = 100; // Initial health of the crystal
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void TakeDamage(int amount)
    {
        // Logic to handle taking damage
        Debug.Log($"Crystal took {amount} damage.");

        health -= amount;
        if (health <= 0)
        {
            DestroyCrystal();
        }
    }

    private void DestroyCrystal()
    {
        // Logic to handle crystal destruction
        Debug.Log("Crystal destroyed.");
        Destroy(gameObject); // Destroys the crystal GameObject
    }
}
