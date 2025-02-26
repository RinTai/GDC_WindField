using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//做一个精细的草 感觉需要用到骨骼 布料模拟等
public class Grass : MonoBehaviour
{
    Mesh mesh;
    // Start is called before the first frame update
    void Start()
    {
     mesh = GetComponent<MeshFilter>().mesh;   
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
