using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [SerializeField] Renderer[] targetRenderers;
    [SerializeField] int bodyMatIndex = 0;

    private WizardData wizardData;
    private bool isInitialized = false;

    public void Setup(WizardData data)
    {
        wizardData = data;
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        ApplyAppearance();
        isInitialized = true;
    }

    private void ApplyAppearance()
    {
        if (wizardData == null || wizardData.material == null)
        {
            Debug.LogWarning($"[PlayerAppearance] Missing WizardData or material on {gameObject.name}.");
            return;
        }

        foreach (var rend in targetRenderers)
        {
            if (rend == null) continue;

            var mats = rend.sharedMaterials;
            if (mats.Length == 1)
            {
                rend.sharedMaterial = wizardData.material;
            }
            else if (bodyMatIndex >= 0 && bodyMatIndex < mats.Length)
            {
                mats[bodyMatIndex] = wizardData.material;
                rend.sharedMaterials = mats;
            }
        }
    }
}
