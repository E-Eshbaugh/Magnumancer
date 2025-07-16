using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [SerializeField] Renderer[] targetRenderers;
    [SerializeField] int playerSlot = 0;        // 0-based; set in inspector or at spawn
    [SerializeField] int bodyMatIndex = 0;      // which sub-material to replace if multi-material

    void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        // Optional: auto-derive slot from tag "Player1".."Player4"
        // (comment out if you prefer to assign manually)
        if (gameObject.CompareTag("Player1")) playerSlot = 0;
        else if (gameObject.CompareTag("Player2")) playerSlot = 1;
        else if (gameObject.CompareTag("Player3")) playerSlot = 2;
        else if (gameObject.CompareTag("Player4")) playerSlot = 3;
    }

    void Start()
    {
        ApplyAppearance();
    }

    // If you spawn players dynamically and know the slot later:
    public void Init(int slot)
    {
        playerSlot = slot;
        ApplyAppearance();
    }

    void ApplyAppearance()
    {
        var dm = DataManager.Instance;
        if (dm == null) { Debug.LogWarning("PlayerAppearance: no DataManager."); return; }

        var wiz = dm.GetWizard(playerSlot);
        if (wiz == null || wiz.material == null)
        {
            Debug.LogWarning($"PlayerAppearance: no WizardData/material for slot {playerSlot}.");
            return;
        }

        foreach (var rend in targetRenderers)
        {
            // Single-material renderer?
            if (rend.sharedMaterials.Length == 1)
            {
                rend.sharedMaterial = wiz.material;
            }
            else
            {
                // Replace just the body slot (bodyMatIndex)
                var mats = rend.sharedMaterials;
                if (bodyMatIndex >= 0 && bodyMatIndex < mats.Length)
                    mats[bodyMatIndex] = wiz.material;
                rend.sharedMaterials = mats;
            }
        }
    }
}
