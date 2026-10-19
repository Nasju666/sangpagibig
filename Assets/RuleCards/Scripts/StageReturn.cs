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
        if (!nearby || MouseLook.isPaused || !Input.GetKeyDown(KeyCode.E)) return;
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
        if (!nearby) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 540f, 65f),
            "Return to the Archive\nPress E" + (string.IsNullOrEmpty(message) ? "" : " — " + message));
        ResponsiveIMGUI.End(previous);
    }


void Start()
    {
        var beacon = GetComponent<ArchiveReturnBeacon>();
        if (beacon == null) beacon = gameObject.AddComponent<ArchiveReturnBeacon>();
        beacon.EnsureCue(this);
    }
}