using UnityEngine;

public class TerminalReveal : MonoBehaviour
{
    public GameObject[] revealedObjects;
    public string prompt = "Stabilize the Archive geometry";
    public bool startHidden = true;
    private bool nearby;
    private bool activated;

    void Start()
    {
        if (startHidden) foreach (GameObject item in revealedObjects) if (item != null) item.SetActive(false);
    }
    void Update()
    {
        if (nearby && !activated && Input.GetKeyDown(KeyCode.E))
        {
            activated = true;
            foreach (GameObject item in revealedObjects) if (item != null) item.SetActive(true);
        }
    }
    void OnTriggerEnter(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = true; }
    void OnTriggerExit(Collider other) { if (other.GetComponent<CharacterController>() != null) nearby = false; }
    void OnGUI() { if (nearby && !activated) GUI.Box(new Rect(20, Screen.height - 95, 430, 65), prompt + "\nPress E"); }
}