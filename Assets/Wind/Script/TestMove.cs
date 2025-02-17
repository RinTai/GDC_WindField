using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TestMove : MonoBehaviour
{
    // Start is called before the first frame update
    Transform mTrans;

    public float speed = 1f;
    public Vector3 velocity = Vector3.zero;
    public float radius = 3f;
    public float progress = 0f;
    void Awake()
    {
        mTrans = transform;
    }

    // Update is called once per frame
    [System.Obsolete]
    void Update()
    {
        progress += Time.deltaTime * speed;

        if (progress >= 360) {
            progress -= 360;
        }
        float x1 =  0 + radius * Mathf.Cos(progress);
        float y1 = 0 + radius* Mathf.Sin(progress);
        velocity = new Vector3(x1, 0, y1) - this.transform.position;
        //this.transform.position = new Vector3(x1, 0, y1);
        this.transform.Rotate(0, speed * Time.deltaTime, 0);
        
    }
}
