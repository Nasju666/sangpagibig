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
        if (nearby && !MouseLook.isPaused && Input.GetKeyDown(KeyCode.E))
        {
            if (unlocked && !string.IsNullOrEmpty(destinationScene)) { RuleCardsAudio.Play("portal", .9f); SceneManager.LoadScene(RuleCardsScenes.Resolve(destinationScene)); }
            else { RuleCardsAudio.Play("deny", .5f); message = "Required Rule Card missing: " + requiredCard; }
        }
    }

    void OnTriggerEnter(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = true; }
    void OnTriggerExit(Collider other) { if (other.GetComponent<CharacterController>() != null) { nearby = false; message = ""; } }
void OnGUI()
    {
        if (!nearby || MouseLook.isPaused) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        string state = RuleCardsProgress.HasCard(requiredCard) ? "Press E to enter" : "LOCKED";
        GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 480f, 65f),
            portalLabel + "\n" + state + (string.IsNullOrEmpty(message) ? "" : " — " + message));
        ResponsiveIMGUI.End(previous);
    }
}