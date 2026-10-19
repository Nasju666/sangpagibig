using UnityEngine;

public class RuleCardPickup : MonoBehaviour
{
    public string cardId;
    public string displayName = "Rule Card";
    public Renderer[] glowRenderers;
    private bool nearby;
    private bool collected;

    /// <summary>Visible in its vault but not collectable until the vault opens.</summary>
    public bool Locked { get; private set; }

    /// <summary>
    /// Vault scripts call this instead of hiding the reward: a Rule Card stays on show behind the
    /// bars (locked) and becomes collectable when the vault opens. Other rewards just toggle.
    /// </summary>
    public static void SetVaultOpen(GameObject reward, bool open)
    {
        if (reward == null) return;
        var pickup = reward.GetComponent<RuleCardPickup>();
        if (pickup == null) { reward.SetActive(open); return; }
        reward.SetActive(true);
        pickup.Locked = !open;
    }

    void Start()
    {
        collected = RuleCardsProgress.HasCard(cardId);
        if (collected) SetVisible(false);
    }

void Update()
    {
        if (nearby && !collected && !Locked && !MouseLook.isPaused && Input.GetKeyDown(KeyCode.E)) TryCollect();
    }

    public bool TryCollect()
    {
        if (!nearby || collected || Locked) return false;
        // A stale pickup must not replay feedback or re-grant a saved card.
        if (RuleCardsProgress.HasCard(cardId))
        {
            collected = true;
            nearby = false;
            SetVisible(false);
            return false;
        }
        RuleCardsProgress.GrantCard(cardId);
        collected = true;
        nearby = false;
        RuleCardCelebration.Play(transform.position, displayName);
        RuleCardsStory.Play("card_" + cardId, null, true, 1.8f);
        SetVisible(false);
        return true;
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
        if(!nearby || collected || MouseLook.isPaused) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 430f, 65f),
            displayName + (Locked ? "\nSealed in the vault. Open it first." : "\nPress E to archive this card"));
        ResponsiveIMGUI.End(previous);
    }
}