
using UnityEngine;

//三种风力发动机
public enum MotorType
{
    Directional,
    Omni,
    Vortex,
    Moving,
    Mesh,
}

public struct MotorDirectional
{
    public Vector3 position;
    public float radiusSq;
    public Vector3 force;
}

public struct MotorOmni
{
    public Vector3 position;
    public float radiusSq;
    public float force;
}

public struct MotorVortex
{
    public Vector3 position;
    public Vector3 axis;
    public float radiusSq;
    public float force;
}

public struct MotorMoving
{
    public Vector3 prePosition;
    public float moveLen;
    public Vector3 moveDir;
    public float radiusSq;
    public float force;
}
public struct MotorCylinder
{
    public Vector3 position;
    public Vector3 axis;
    public float height;
    public float radiusBottonSq;
    public float radiusTopSq;
    public float force;
}


/// <summary>
/// 发动机有时存在有时候消散
/// </summary>
[DefaultExecutionOrder(410)]
public class WindMotor : MonoBehaviour
{
    public MotorType MotorType;
    public MotorDirectional motorDirectional;
    public MotorOmni motorOmni;
    public MotorVortex motorVortex;
    public MotorMoving motorMoving;
    public MotorCylinder motorCylinder;

    private static MotorDirectional emptyMotorDirectional = new MotorDirectional();
    private static MotorOmni emptyMotorOmni = new MotorOmni();
    private static MotorVortex emptyMotorVortex = new MotorVortex();
    private static MotorMoving emptyMotorMoving = new MotorMoving();
    private static MotorCylinder emptyMotorCylinder = new MotorCylinder();
    public static MotorDirectional GetEmptyMotorDirectional()
    {
        return emptyMotorDirectional;
    }
    public static MotorOmni GetEmptyMotorOmni()
    {
        return emptyMotorOmni;
    }
    public static MotorVortex GetEmptyMotorVortex()
    {
        return emptyMotorVortex;
    }
    public static MotorMoving GetEmptyMotorMoving()
    {
        return emptyMotorMoving;
    }
    public static MotorCylinder GetEmptyMotorCylinder()
    {
        return emptyMotorCylinder;
    }


    //创建时间
    private float m_CreateTime;
    public bool Loop = true;
    public float LifeTime = 5f;
    [Range(0.001f, 10f)]
    public float Radius = 1f;
    public AnimationCurve RadiusCurve = AnimationCurve.Linear(1, 1, 1, 1);
    public Vector3 Asix = Vector3.up;
    [Range(-12f, 12f)]
    public float Force = 1f;
    public Vector3 ForceDir = Vector3.up;
    public AnimationCurve ForceCurve = AnimationCurve.Linear(1, 1, 1, 1);
    public float Duration = 0f;
    public float MoveLength;
    public AnimationCurve MoveLengthCurve = AnimationCurve.Linear(1, 1, 1, 1);

    private Vector3 m_prePosition = Vector3.zero;
    private void Start()
    {
        m_CreateTime = Time.fixedTime;
    }

    private void OnEnable()
    {
        WindManager.Instance.AddWindMotor(this);
       
        // WindRender.m_WindFieldPass.AddWindMotor(this);
        m_CreateTime = Time.fixedTime;
    }

    private void OnDisable()
    {
        WindManager.Instance.RemoveWindMotor(this);
        // WindRender.m_WindFieldPass.RemoveWindMotor(this);
    }

    /// <summary>
    /// 在特定点时的力量 可以变化的力
    /// </summary>
    /// <param name="timePerc"></param>
    /// <returns></returns>
    private float GetForce(float timePerc)
    {
        return Mathf.Clamp(ForceCurve.Evaluate(timePerc) * Force, -12f, 12f);
    }

    /// <summary>
    /// 在风调用结束时移除他
    /// </summary>
    public void CheckMotor()
    {
        float dt = Time.fixedTime - m_CreateTime;
        if ((dt > LifeTime))
        {
            if (Loop)
            {
                m_CreateTime = Time.fixedTime;
            }
            else
            {
                m_CreateTime = 0f;
                //这里改成延迟修改
                WindManager.Instance.RemoveWindMotor(this);
                WindRender.m_WindFieldPass.RemoveWindMotor(this);
            }
        }
    }
    /// <summary>
    /// 风在使用时才更新数据
    /// </summary>
    public void UpdateWindMotor()
    {
        switch (MotorType)
        {
            case MotorType.Directional:
                UpdateDirectionalWind();
                break;
            case MotorType.Omni:
                UpdateOmniWind();
                break;
            case MotorType.Vortex:
                UpdateVortexWind();
                break;
            case MotorType.Moving:
                UpdateMovingWind();
                break;
        }
    }
    /// <summary>
    /// 方向风
    /// </summary>
    private void UpdateDirectionalWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorDirectional = new MotorDirectional()
        {
            position = transform.position,
            radiusSq = radius * radius,
            force = transform.forward * ForceCurve.Evaluate(timePerc) * Force
        };
        CheckMotor();
    }
    /// <summary>
    /// 点风
    /// </summary>
    private void UpdateOmniWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorOmni = new MotorOmni()
        {
            position = transform.position,
            radiusSq = radius * radius,
            force = ForceCurve.Evaluate(timePerc) * Force
        };
        CheckMotor();
    }
    /// <summary>
    /// 旋转的风 不知道叫啥
    /// </summary>
    private void UpdateVortexWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorVortex = new MotorVortex()
        {
            position = transform.position,
            axis = Vector3.Normalize(Asix),
            radiusSq = radius * radius,
            force = ForceCurve.Evaluate(timePerc) * Force
        };
        CheckMotor();
    }
    /// <summary>
    /// 运动时的松果形状
    /// </summary>
    private void UpdateMovingWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float moveLen = MoveLength * MoveLengthCurve.Evaluate(timePerc);
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        Vector3 position = transform.position;
        Vector3 prePosition = m_prePosition == Vector3.zero ? position : m_prePosition;
        Vector3 moveDir = position - prePosition;
        motorMoving = new MotorMoving()
        {
            prePosition = prePosition,
            moveLen = moveLen,
            moveDir = moveDir,
            radiusSq = radius * radius,
            force = GetForce(timePerc),
        };
        m_prePosition = position;
        CheckMotor();
    }
}
