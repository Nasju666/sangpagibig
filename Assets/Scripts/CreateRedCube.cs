using UnityEngine;

public class CreateRedCube : MonoBehaviour
{
    void Start()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(0, 2, 0);
        cube.GetComponent<Renderer>().material.color = Color.red;
    }
}
