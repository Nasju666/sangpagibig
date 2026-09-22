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
void OnGUI()
    {
        if(!nearby || activated) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 460f, 65f), prompt + "\nPress E");
        ResponsiveIMGUI.End(previous);
    }
}