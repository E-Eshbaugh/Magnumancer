using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [SerializeField] Renderer[] targetRenderers;
    [SerializeField] int bodyMatIndex = 0;
    [SerializeField]public bool makeTransparent = false;
    [SerializeField] float alphaOverride = 0.4f;

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
            Material matInstance = new Material(wizardData.material);

            if (makeTransparent)
            {
                // URP/Lit-specific: convert to transparent mode
                matInstance.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                matInstance.SetFloat("_ZWrite", 0);
                matInstance.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                matInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                // Adjust alpha in Base Map
                if (matInstance.HasProperty("_BaseColor"))
                {
                    Color baseColor = matInstance.GetColor("_BaseColor");
                    baseColor.a = alphaOverride;
                    matInstance.SetColor("_BaseColor", baseColor);
                }
            }

            if (mats.Length == 1)
            {
                rend.material = matInstance;
            }
            else if (bodyMatIndex >= 0 && bodyMatIndex < mats.Length)
            {
                mats[bodyMatIndex] = matInstance;
                rend.materials = mats;
            }
        }
    }

}
