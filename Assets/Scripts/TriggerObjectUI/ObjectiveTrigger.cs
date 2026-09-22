using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ObjectiveTrigger : MonoBehaviour
{
    [TextArea] public string newObjective = "Find the control room";
    public bool triggerOnce = true;

    private bool triggered = false;

    private void Reset()
    {
        // ensure trigger collider is set as trigger
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered && triggerOnce) return;
        if (!other.CompareTag("Player")) return;

        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ShowObjective(newObjective);
        }
        else
        {
            Debug.LogWarning("ObjectiveManager not found in scene.");
        }

        triggered = true;

        if (triggerOnce)
        {
            // optional: disable the gameObject so it won't retrigger
            gameObject.SetActive(false);
        }
    }
}