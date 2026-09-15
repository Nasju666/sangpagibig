using UnityEngine;
using UnityEngine.SceneManagement;

public class StageReturn : MonoBehaviour
{
    public string hubScene = "ArchiveHub";
    public string stageId;
    public string requiredCard;
    private bool nearby;
    private string message;

    void Update()
    {
        if (!nearby || !Input.GetKeyDown(KeyCode.E)) return;
        if (!RuleCardsProgress.HasCard(requiredCard))
        {
            message = "The Archive will not release you. Recover the Rule Card.";
            return;
        }
        RuleCardsProgress.CompleteStage(stageId);
        SceneManager.LoadScene(hubScene);
    }

    void OnTriggerEnter(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = true; }
    void OnTriggerExit(Collider other) { if (other.GetComponent<CharacterController>() != null) { nearby = false; message = ""; } }
    void OnGUI()
    {
        if (nearby) GUI.Box(new Rect(20, Screen.height - 95, 520, 65), "Return to the Archive\nPress E" + (string.IsNullOrEmpty(message) ? "" : " — " + message));
    }
}