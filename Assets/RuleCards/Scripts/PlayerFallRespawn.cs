using UnityEngine;

public class PlayerFallRespawn : MonoBehaviour
{
    public float fallThreshold = -18f;
    private Vector3 checkpoint;
    private CharacterController controller;
    void Start() { checkpoint = transform.position; controller = GetComponent<CharacterController>(); }
    void Update()
    {
        if (transform.position.y >= fallThreshold) return;
        if (controller != null) controller.enabled = false;
        transform.position = checkpoint;
        if (controller != null) controller.enabled = true;
    }
}