using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugVertics : MonoBehaviour
{
    // Start is called before the first frame update
    Mesh mesh;
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;

        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            Debug.Log(mesh.vertices[i]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
