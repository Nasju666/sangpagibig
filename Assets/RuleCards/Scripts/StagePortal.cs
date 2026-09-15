using UnityEngine;
using UnityEngine.SceneManagement;

public class StagePortal : MonoBehaviour
{
    public string destinationScene;
    public string portalLabel = "Stage";
    public string requiredCard;
    public Renderer statusRenderer;
    public Color availableColor = new Color(0f, 1f, 1f, 1f);
    public Color lockedColor = new Color(0.35f, 0.12f, 0.06f, 1f);
    private bool nearby;
    private string message;

void Update()
    {
        bool unlocked = RuleCardsProgress.HasCard(requiredCard);
        if (statusRenderer != null)
        {
            Material portalMaterial = statusRenderer.material;
            Color stateColor = unlocked ? availableColor : lockedColor;
            if (portalMaterial.HasProperty("_BaseColor")) portalMaterial.SetColor("_BaseColor", stateColor);
            if (portalMaterial.HasProperty("_Color")) portalMaterial.SetColor("_Color", stateColor);
            if (portalMaterial.HasProperty("_EmissionColor"))
                portalMaterial.SetColor("_EmissionColor", stateColor * (unlocked ? 3f : 1.5f));
        }
        if (nearby && Input.GetKeyDown(KeyCode.E))
        {
            if (unlocked && !string.IsNullOrEmpty(destinationScene)) SceneManager.LoadScene(destinationScene);
            else message = "Required Rule Card missing: " + requiredCard;
        }
    }

    void OnTriggerEnter(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = true; }
    void OnTriggerExit(Collider other) { if (other.GetComponent<CharacterController>() != null) { nearby = false; message = ""; } }
    void OnGUI()
    {
        if (!nearby) return;
        string state = RuleCardsProgress.HasCard(requiredCard) ? "Press E to enter" : "LOCKED";
        GUI.Box(new Rect(20, Screen.height - 95, 430, 65), portalLabel + "\n" + state + (string.IsNullOrEmpty(message) ? "" : " — " + message));
    }
}