using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEngine;


//风发动机的对象池
public class WindPool : MonoBehaviour
{
    private static WindPool m_Instance;

    public static WindPool Instance
    {
        get
        {
            return m_Instance;
        }
    }
    private List<GameObject> m_WindPool;
    public Transform WindContainer; 
    public GameObject WindMotorPrefab;
    private int windMotorCurNum = 0;
    private int maxinumNum = 10;


    public GameObject PopWindMotor()
    {
        GameObject output;
        if(windMotorCurNum >= 0)
        {
            windMotorCurNum--;
            output = m_WindPool[windMotorCurNum];
            m_WindPool.RemoveAt(windMotorCurNum);
        }
        else
        {
            output = new GameObject("WindMotor");
            output.transform.SetParent(WindContainer);
            output.AddComponent<WindMotor>();
        }
        output.SetActive(true);
        return output;
    }

    public void PushWindMotor(GameObject input)
    {
        input.SetActive(false);
        if (windMotorCurNum < maxinumNum)
        {
            windMotorCurNum++;
            m_WindPool.Add(input);
        }
        else
        {
            Object.DestroyImmediate(input);
        }
    }
}
