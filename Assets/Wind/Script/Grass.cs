using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//��һ����ϸ�Ĳ� �о���Ҫ�õ����� ����ģ���
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
