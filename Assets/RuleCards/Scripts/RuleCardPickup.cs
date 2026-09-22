using UnityEngine;

public class RuleCardPickup : MonoBehaviour
{
    public string cardId;
    public string displayName = "Rule Card";
    public Renderer[] glowRenderers;
    private bool nearby;
    private bool collected;

    void Start()
    {
        collected = RuleCardsProgress.HasCard(cardId);
        if (collected) SetVisible(false);
    }

    void Update()
    {
        if (nearby && !collected && Input.GetKeyDown(KeyCode.E))
        {
            RuleCardsProgress.GrantCard(cardId);
            collected = true;
            nearby = false;
            SetVisible(false);
        }
    }

void SetVisible(bool value)
    {
        foreach (Renderer r in glowRenderers) if (r != null) r.enabled = value;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = value;
    }

    void OnTriggerEnter(Collider other) { if (!collected && other.GetComponent<CharacterController>() != null) nearby = true; }
    void OnTriggerExit(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = false; }
void OnGUI()
    {
        if(!nearby || collected) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 430f, 65f),
            displayName + "\nPress E to archive this card");
        ResponsiveIMGUI.End(previous);
    }
}