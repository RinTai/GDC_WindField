
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework.Internal;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UIElements;






//可以认为是每个顶点的质点 （基本每个顶点都有 剩下的都是加）
public struct Particle
{
    public float Mass { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }

    public float padding;
    public Particle(Vector3 position,float mass)
    {
        Position = position;
        Mass = mass;
        Velocity = new Vector3(0, 0, 0);
        padding = 0;
    }
}





/// <summary>
/// 一种约束 每种约束 需要走完所有的顶点 相当于有n个顶点 这一种约束需要进行 Dispatch 至少n个线程去处理顶点。还要算梯度上面的 约束的条件写在ComputeShader里 这个类是用来执行每种约束(每种里面还有至少n个约束)的
/// </summary>
public class Constraint
{

    protected static string m_NowPosBufferName = "nowPoint";
    protected static string m_PrePosBufferName = "prePoint";
    protected static string m_PostPosBufferName = "postPoint";

    protected int VertexNum = 0;
    public string ConstraintKernelName = "Constraint";
    public int ConstraintKernelIndex = 0;
    public CommandBuffer ConstraintCmd { get; set; }

    public ComputeShader   ConstraintCompute { get; set; }

    //基础3Buffer 
    public ComputeBuffer m_PrePointBuffer;
    public ComputeBuffer m_PostPointBuffer;
    public ComputeBuffer m_NowPointBuffer;

    public Constraint()
    {
     
    }
    /// <summary>
    /// 初始化Buffer的 传入3Buffer就行了？有的是不同的
    /// </summary>
    public virtual void InitialBuffer(ComputeBuffer nowPosBuffer,ComputeBuffer prePosBuffer,ComputeBuffer postPosBuffer)
    {
        m_PrePointBuffer = prePosBuffer;
        m_NowPointBuffer = nowPosBuffer;
        m_PostPointBuffer = postPosBuffer;
    }
    /// <summary>
    /// 约束的执行，具体实现写在update里了. 不同的约束不同执行方式吧，这个是最基本的
    /// </summary>
    /// <param name="dt"></param>
    public virtual void Execute(float dt, int DispatchNumX, int DispatchNumY, int DispatchNumZ)
    {
        //示例
        /*
        ConstraintCmd.DispatchCompute(ConstraintCompute, ConstraintKernelIndex, DispatchNumX, DispatchNumY, DispatchNumZ);
        */
    }
     public void InitialVertexCount(int Count)
    {
        VertexNum = Count;
    }
}








/// <summary>
/// 距离约束的约束类
/// </summary>
public class Constraint_Distance : Constraint
{
    /// <summary>
    /// 每一个约束自己的粒子类
    /// </summary>
    public struct CustomParticle 
    {
        public float Mass { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }

        public float padding;
        public CustomParticle(Vector3 position, float mass)
        {
            Position = position;
            Mass = mass;
            Velocity = new Vector3(0, 0, 0);
            padding = 0;
        }

    }

    int testIndex;
    int testIndex_2;
    int testIndex_3;
    int testIndex_4;
    public Constraint_Distance(CommandBuffer cmd, string kernelName, ComputeShader computeShader)
    {
        ConstraintCompute = computeShader;
        ConstraintCmd = cmd;
        ConstraintKernelName = kernelName;
        ConstraintKernelIndex = ConstraintCompute.FindKernel(ConstraintKernelName);
        testIndex = ConstraintCompute.FindKernel("Constraint_Size");
        testIndex_2 = ConstraintCompute.FindKernel("Constraint_Fixed");
        testIndex_3 = ConstraintCompute.FindKernel("Constraint_Bend");
        testIndex_4 = ConstraintCompute.FindKernel("Constraint_SelfCollsion");
    }
    public override void Execute(float dt, int DispatchNumX, int DispatchNumY, int DispatchNumZ)
    {

        ConstraintCompute.SetFloat("alpha", 0.1f / (Time.fixedDeltaTime * Time.fixedDeltaTime));
        ConstraintCompute.SetFloat("deltaTime", Time.deltaTime / 20f);
        ConstraintCompute.SetInt("simulationTimes", 8);
        ConstraintCompute.SetInt("meshVertexNums", VertexNum);
        ConstraintCompute.SetInt("rawCount", (int)Mathf.Sqrt(VertexNum));

        //没找到哪里出问题了现在
        ConstraintCmd.BeginSample("Constraint");
   

        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, ConstraintKernelIndex, m_PrePosBufferName, m_PrePointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, ConstraintKernelIndex, m_NowPosBufferName, m_NowPointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, ConstraintKernelIndex, m_PostPosBufferName, m_PostPointBuffer);
        ConstraintCmd.DispatchCompute(ConstraintCompute, ConstraintKernelIndex, DispatchNumX, DispatchNumY, DispatchNumZ);

        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex, m_PrePosBufferName, m_PrePointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex, m_NowPosBufferName, m_NowPointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex, m_PostPosBufferName, m_PostPointBuffer);
        ConstraintCmd.DispatchCompute(ConstraintCompute, testIndex, DispatchNumX, DispatchNumY, DispatchNumZ);

        //感觉这个更像不可塑的约束 mesh不能压缩只能抖动
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_2, m_PrePosBufferName, m_PrePointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_2, m_NowPosBufferName, m_NowPointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_2, m_PostPosBufferName, m_PostPointBuffer);
        ConstraintCmd.DispatchCompute(ConstraintCompute, testIndex_2, DispatchNumX, DispatchNumY, DispatchNumZ);

        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_3, m_PrePosBufferName, m_PrePointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_3, m_NowPosBufferName, m_NowPointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_3, m_PostPosBufferName, m_PostPointBuffer);
        ConstraintCmd.DispatchCompute(ConstraintCompute, testIndex_3, DispatchNumX, DispatchNumY, DispatchNumZ);

        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_4, m_PrePosBufferName, m_PrePointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_4, m_NowPosBufferName, m_NowPointBuffer);
        ConstraintCmd.SetComputeBufferParam(ConstraintCompute, testIndex_4, m_PostPosBufferName, m_PostPointBuffer);
        ConstraintCmd.DispatchCompute(ConstraintCompute, testIndex_4, DispatchNumX, DispatchNumY, DispatchNumZ);

        ConstraintCmd.EndSample("Constraint");
        //可能还需要设置一些？       
    }
}









/// <summary>
///  d_lambda = (-C - alpha*lambda) / (gradC * w_T * gradC_T + alpha)
///  x = x + deltaX where deltaX = gradC * w_T(i) * lambda
/// </summary>
public class XPBD : MonoBehaviour
{
    private static int MAXVERTEX = 65536;
    private string m_InteractionName = "Interaction";
    private string m_CalculateName = "Calculate";
    static string m_NowPosBufferName = "nowPoint";
    static string m_PrePosBufferName = "prePoint";
    static string m_PostPosBufferName = "postPoint";


    public Mesh testMesh;
    //距离的迭代约束项.
    public Constraint_Distance m_Constraint_Distance;
    public CommandBuffer cmd;
    public ComputeShader m_ComputeShader;
    //布料与外力交互的项(为速度什么赋值什么的，毕竟是XPBD)
    public ComputeShader m_InteractionCS;
    //布料最后的总结项(统计速度什么的，毕竟是XPBD)
    public ComputeShader m_CalculateCS;

    //基础的3Buffer (分别代表初始位置 上一次的位置)
    public ComputeBuffer m_PrePointBuffer;
    public ComputeBuffer m_PostPointBuffer;
    public ComputeBuffer m_NowPointBuffer;

    private List<Particle> m_PointList;
    Particle[] particles;


    int VertexNum;
    int DispatchNumX;
    int DispatchNumY;
    int DispatchNumZ;

    int num = 0;

    /// <summary>
    /// 初始化3Buffer
    /// </summary>
    private void Initialize3Buffer()
    {
        m_NowPointBuffer = new ComputeBuffer(MAXVERTEX,Marshal.SizeOf<Particle>());
        m_PrePointBuffer = new ComputeBuffer(MAXVERTEX, Marshal.SizeOf<Particle>());
        m_PostPointBuffer = new ComputeBuffer(MAXVERTEX, Marshal.SizeOf<Particle>());
    }


    private void Awake()
    {
        Initialize3Buffer();

        testMesh = GetComponent<MeshFilter>().mesh;
        cmd = new CommandBuffer();
        m_Constraint_Distance = new Constraint_Distance(cmd, "Constraint", m_ComputeShader);
        m_PointList = new List<Particle>();
        cmd.name = "Constraint";

        
        //需不需要转世界坐标？
        for (int i = 0;i<testMesh.vertices.Length;i++)
        {
            //
            if (i == 0 || i == (int)Mathf.Sqrt(testMesh.vertexCount) - 1)
            {
                if(i == (int)Mathf.Sqrt(testMesh.vertexCount) - 1)
                {
                    testMesh.vertices[i] = new Vector3(0, 0, 5f);
                }
                    m_PointList.Add(new Particle(testMesh.vertices[i], 0f));
            }
            else
                 m_PointList.Add(new Particle(testMesh.vertices[i], 1f));
        }

        particles = new Particle[testMesh.vertices.Length];
        
        
        //初始化DisPatch
        VertexNum = m_PointList.Count;
        DispatchNumX = Mathf.Min(VertexNum, 1024) / 64 + 1;
        DispatchNumY = VertexNum / 1024 + 1;
        DispatchNumZ = VertexNum / (1024 * 1024) + 1;

        //初始化ComputeBuffer
        m_PrePointBuffer.SetData(m_PointList.ToArray());
        m_NowPointBuffer.SetData(m_PointList.ToArray());
        m_PostPointBuffer.SetData(m_PointList.ToArray());

        m_Constraint_Distance.InitialVertexCount(testMesh.vertices.Length);
        //初始化初始位置.
        m_Constraint_Distance.InitialBuffer(m_NowPointBuffer, m_PrePointBuffer, m_PostPointBuffer);

        InitialInteraciton();
        InitialCalculate();
    }

    private void FixedUpdate()
    {
        num++;
        {
            m_NowPointBuffer.GetData(particles);


            Vector3[] temp = new Vector3[m_PointList.Count];

            for (int i = 0; i < testMesh.vertexCount; i++)
            {
                temp[i] = particles[i].Position;
            }
                testMesh.vertices = temp;

            testMesh.RecalculateBounds();

            testMesh.RecalculateNormals();


            cmd.SetBufferData(m_NowPointBuffer, particles);
        }

            cmd.BeginSample("ClothCompute");
            for (int i = 0; i < 20; i++)
            {

                //交互项执行
                InteractionExecute();
                //这中间是约束项执行
                {
                    m_Constraint_Distance.Execute(Time.deltaTime, DispatchNumX, DispatchNumY, DispatchNumZ);
                }
                //最后速度结算执行
                CalculateExecute();
            }
        
        cmd.EndSample("ClothCompute");
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Clear();

    }

    /// <summary>
    /// 初始化交互计算的CS
    /// </summary>
    private void InitialInteraciton()
    {
        m_InteractionCS.SetFloat("deltaTime", Time.deltaTime / 20f);
        m_InteractionCS.SetInt("meshVertexNums", VertexNum);
        m_InteractionCS.SetInt("rawCount", (int)Mathf.Sqrt(VertexNum));
        m_InteractionCS.SetFloat("VoxelSize", WindManager.VoxelSize);
        m_InteractionCS.SetVector("WindCenterPos", this.transform.position);
        m_InteractionCS.SetVector("WindFieldSize", WindManager.Instance.GetWindFieldSize());

    }
    /// <summary>
    /// 执行交互计算的CS
    /// </summary>
    private void InteractionExecute()
    {
        Matrix4x4 temp = this.transform.localToWorldMatrix;
        m_InteractionCS.SetMatrix("LocalToWorld", temp);
        
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetColumn(0, temp.GetColumn(0).normalized);
        rotationMatrix.SetColumn(1, temp.GetColumn(1).normalized);
        rotationMatrix.SetColumn(2, temp.GetColumn(2).normalized);
        rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // 忽略平移

        m_InteractionCS.SetMatrix("RotationWorldToLoacl", rotationMatrix);
      
        int InteractionKernelIndex = m_InteractionCS.FindKernel(m_InteractionName);
        cmd.SetComputeTextureParam(m_InteractionCS, InteractionKernelIndex, "WindField", WindManager.Instance.GetWindField());
        cmd.SetComputeBufferParam(m_InteractionCS, InteractionKernelIndex, m_PrePosBufferName, m_PrePointBuffer);
        cmd.SetComputeBufferParam(m_InteractionCS, InteractionKernelIndex, m_NowPosBufferName, m_NowPointBuffer);
        cmd.SetComputeBufferParam(m_InteractionCS, InteractionKernelIndex, m_PostPosBufferName, m_PostPointBuffer);
        cmd.DispatchCompute(m_InteractionCS, InteractionKernelIndex, DispatchNumX, DispatchNumY, DispatchNumZ);
    }

    /// <summary>
    /// 执行结算项的初始化,
    /// </summary>
    private void InitialCalculate()
    {
        m_CalculateCS.SetFloat("deltaTime", Time.deltaTime / 20f);
        m_CalculateCS.SetInt("meshVertexNums", VertexNum);
        m_CalculateCS.SetInt("rawCount", (int)Mathf.Sqrt(VertexNum));
    }
    /// <summary>
    /// 执行最后速度计算的CS
    /// </summary>
    private void CalculateExecute()
    {       
        int CalculateKernelIndex = m_CalculateCS.FindKernel(m_CalculateName);
        cmd.SetComputeBufferParam(m_CalculateCS, CalculateKernelIndex, m_PrePosBufferName, m_PrePointBuffer);
        cmd.SetComputeBufferParam(m_CalculateCS, CalculateKernelIndex, m_NowPosBufferName, m_NowPointBuffer);
        cmd.SetComputeBufferParam(m_CalculateCS, CalculateKernelIndex, m_PostPosBufferName, m_PostPointBuffer);
        cmd.DispatchCompute(m_CalculateCS, CalculateKernelIndex, DispatchNumX, DispatchNumY, DispatchNumZ);
    }
}