
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using MatrixEigen;
using Unity.Mathematics;




//这一套是没加入RenderFeature的WindField 
struct WindGridCell
{
   public Vector4 velocity;
   public float pressure;
   public Vector3 padding;
}
[ExecuteAlways]
public class WindManager : MonoBehaviour
{
    public List<GameObject> goTests;
    public Material test;
    public VisualEffect debugParticle;
    public VisualEffect particleTest;
    private const string bufferName = "DispatchBuffer";
    CommandBuffer cmd;
    private int WindFieldSizeX = 256;
    private int WindFieldSizeY = 16;
    private int WindFieldSizeZ = 256;
    private static int MAXMOTOR = 10;
    private static int MAXVERTEX = 65536;
    private static int MAXOBSTACLE = 16;
    private static float VoxelSize = 1f;
    private static float PopVelocity = 2f;
    private static WindManager m_Instance;
    public static WindManager Instance
    {
        get
        {
            return m_Instance;
        }
    }
    public ComputeShader wComputeShader_WindMotor; //CS_1 
    public ComputeShader wComputeShader_Diffusion;
    public ComputeShader wComputeShader_Advect;
    public ComputeShader wComputeShader_Project;
    public ComputeShader wComputeShader_Obstacle_SDF;

    private delegate void DelayedOperation();

    //提供动力的内核句柄
    private int kernelHandle_WindMotor;
    //扩散的CS内核句柄
    private int kernelHandle_Diffusion;//内核句柄
    //平流的内核句柄
    private int kernelHandle_Advect_Positive;
    private int kernelHandle_Advect_Negative;
    //流体去散度的CS句柄
    private int kernelHandle_Project_1;
    private int kernelHandle_Project_2;
    private int kernelHandle_Project_3;
    //用于显示用的句柄
    private int kernelHandle_Test;
    //SDF生成的CS的句柄
    private int kernelHandle_SDF_Create;

    private RenderTexture Test2D;
    //风场的数组，在这里用来测试吧
    private RenderTexture Test3D;
    private RenderTexture windField_Result_Ping;
    private RenderTexture windField_Result_Pong;
    private RenderTexture windField_Div_Pressure_Ping;
    private RenderTexture windField_Div_Pressure_Pong;

    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int ObIndex;
    }
    private struct OBB
    {
        public Matrix4x4 Rotation; //
        public Vector3 Center;// 
        public Vector3 HalfExtents;
    }
    private RenderTexture windField_SDF;//用于记录场内的障碍物的SDF xyz是法线 W是距离
    private List<GameObject> windObstacles = new List<GameObject>(16);
    private List<Vertex> obstacleMeshList = new List<Vertex>();
    private List<OBB> obstacleOBBList = new List<OBB>();
    private List<Vector3> obstacle_OBB_PositionList = new List<Vector3>();
    private List<Vector3> obstacle_OBB_HalfExtentsList = new List<Vector3>();
    private List<Matrix4x4> obstacle_OBB_RotationList = new List<Matrix4x4>();
    private int windObstacleCount = 0;
    private List<WindMotor> windMotors = new List<WindMotor>(MAXMOTOR);
    private List<MotorDirectional> directionalMotorList = new List<MotorDirectional>(MAXMOTOR);
    private int directionalMotor_num = 0;
    private List<MotorOmni> omniMotorList = new List<MotorOmni>(MAXMOTOR);
    private int omniMotor_num = 0;
    private List<MotorVortex> vortexMotorList = new List<MotorVortex>(MAXMOTOR);
    private int vortexMotor_num = 0;
    private List<MotorMoving> movingMotorList = new List<MotorMoving>(MAXMOTOR);
    private int movingMotor_num = 0;

    private ComputeBuffer directionalMotorBuffer;
    private ComputeBuffer omniMotorBuffer;
    private ComputeBuffer vortexMotorBuffer;
    private ComputeBuffer movingMotorBuffer;
    private ComputeBuffer obstacleVertexBuffer;//用于存储顶点的
    private GraphicsBuffer obstacleOBBBuffer;
    private GraphicsBuffer obstacle_OBB_RotationBuffer;
    private GraphicsBuffer obstacle_OBB_PositionBuffer;
    private GraphicsBuffer obstacle_OBB_HalfExtentsBuffer;

    private const string
        kernel_In = "InputResult",
        kernel_Out = "OutputResult",
        kernel_AddForce = "CSAddForce",
        kernel_Diffusion_1 = "CSDiffusion",
        kernel_Advect_Positive = "CSAdvect_Positive",
        kernel_Advect_Negative = "CSAdvect_Negative",
        kernel_Project_1 = "CSProj_1",
        kernel_Project_2 = "CSProj_2",
        kernel_Project_3 = "CSProj_3",
        kernel_Test = "CSFinal",
        kernel_SDF_Create = "SDF_Create",
        kernel_SDF = "Obstacle_SDF",
        deltaTime = "deltaTime",
        wfSpaceMatrix = "WindSpaceMatrix",
        wfSpaceMatrixInv = "InvWindSpaceMatrix",
        Div_Pressure_Input = "Div_Pressure_Input",
        Div_Pressure_Output = "Div_Pressure_Output",
        Directional_BufferId = "DirectionalMotorBuffer",
        Directional_NumId = "DirectionalMotorBufferCount",
        Omni_BufferId = "OmniMotorBuffer",
        Omni_NumId = "OmniMotorBufferCount",
        Vortex_BufferId = "VortexMotorBuffer",
        Vortex_NumId = "VortexMotorBufferCount",
        Moving_BufferId = "MovingMotorBuffer",
        Moving_NumId = "MovingMotorBufferCount",
        Obstacle_Vertex_BufferId = "Obstacle_Vetex_Buffer",
        Obstacle_OBB_BufferId = "Obstacle_OBB_Buffer",
        Obstacle_OBB_Position_BufferId = "Obstacle_OBB_Position_Buffer",
        Obstacle_OBB_Rotation_BufferId = "Obstacle_OBB_Rotation_Buffer",
        Obstacle_OBB_HalfExtents_BufferId = "Obstacle_OBB_HalfExtents_Buffer",
        Obstacle_NumId = "VertexBufferCount";

    private string
        Result_Ping = "Result_Ping",
        Result_Pong = "Result_Pong";


    /*———————————————— */
    public Texture2D windTexture;

    public void Awake()
    {
        m_Instance = this;
        cmd = new CommandBuffer();
        cmd.name = bufferName;
        Application.targetFrameRate = 60;

        kernelHandle_WindMotor = wComputeShader_WindMotor.FindKernel(kernel_AddForce);
        kernelHandle_Diffusion = wComputeShader_Diffusion.FindKernel(kernel_Diffusion_1);
        kernelHandle_Advect_Positive = wComputeShader_Advect.FindKernel(kernel_Advect_Positive);
        kernelHandle_Advect_Negative = wComputeShader_Advect.FindKernel(kernel_Advect_Negative);
        kernelHandle_Project_1 = wComputeShader_Project.FindKernel(kernel_Project_1);
        kernelHandle_Project_2 = wComputeShader_Project.FindKernel(kernel_Project_2);
        kernelHandle_Project_3 = wComputeShader_Project.FindKernel(kernel_Project_3);
        kernelHandle_Test = wComputeShader_WindMotor.FindKernel(kernel_Test);
        kernelHandle_SDF_Create = wComputeShader_Obstacle_SDF.FindKernel(kernel_SDF_Create);


        InitialField();
        InitialComputeShader();
    }

    private void OnEnable()
    {

    }

    public void Update()
    {
        UpdateComputeShader();
        ObstacleDetective();

        WindMotorAdd(); //√
        Diffusion(); //√
        Advect(); //√
        Project(); //√

        cmd.BeginSample("Test");
        //Test
        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_Test, kernel_SDF, windField_SDF);
        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_Test, kernel_In, Test3D);
        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_Test, "Test", Test2D);
        cmd.DispatchCompute(wComputeShader_WindMotor, kernelHandle_Test, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        cmd.EndSample("Test");

        test.SetTexture("_WindField", Test3D);
        Shader.SetGlobalVector("WindFieldCenter", this.transform.position);
        Shader.SetGlobalFloat("VoxelSize", VoxelSize);

        debugParticle.SetTexture("_WindTexture", Test3D);
        debugParticle.SetVector3("_WindCenterPos", this.transform.position);

        particleTest.SetTexture("_WindTexture", Test3D);
        particleTest.SetVector3("_WindCenterPos", this.transform.position);


        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        //这里写纹理去获取，用来Debug看看呢
        windTexture = new Texture2D(Test2D.width, Test2D.height, TextureFormat.RGBAFloat, false);

        RenderTexture.active = Test2D;
        windTexture.ReadPixels(new Rect(0, 0, Test2D.width, Test2D.height), 0, 0);
        windTexture.Apply();
        RenderTexture.active = null;
    }

    public void OnDestroy()
    {
        windField_Div_Pressure_Ping.Release();
        windField_Result_Pong.Release();
        windField_Result_Ping.Release();
        Test3D.Release();
    }
    /// <summary>
    /// 初始化各项风场纹理
    /// </summary>
    void InitialField()
    {
        windField_Result_Ping = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        windField_Result_Pong = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        windField_Div_Pressure_Ping = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        windField_Div_Pressure_Pong = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        windField_SDF = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        Test3D = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);

        windField_Result_Ping.name = "Result_Ping";
        windField_Result_Pong.name = "Result_Pong";
        windField_Div_Pressure_Ping.name = "Div_Pressure_Ping";
        windField_Div_Pressure_Pong.name = "Div_Pressure_Pong";
        windField_SDF.name = "SDF";
        Test3D.name = "FinalResult";

        windField_Result_Ping.dimension = TextureDimension.Tex3D;
        windField_Result_Pong.dimension = TextureDimension.Tex3D;
        windField_Div_Pressure_Ping.dimension = TextureDimension.Tex3D;
        windField_Div_Pressure_Pong.dimension = TextureDimension.Tex3D;
        windField_SDF.dimension = TextureDimension.Tex3D;
        Test3D.dimension = TextureDimension.Tex3D;

        windField_Result_Ping.format = RenderTextureFormat.ARGBHalf;
        windField_Result_Pong.format = RenderTextureFormat.ARGBHalf;
        windField_Div_Pressure_Ping.format = RenderTextureFormat.ARGBHalf;
        windField_Div_Pressure_Pong.format = RenderTextureFormat.ARGBHalf;
        windField_SDF.format = RenderTextureFormat.ARGBHalf;
        Test3D.format = RenderTextureFormat.ARGBHalf;

        windField_Result_Ping.volumeDepth = WindFieldSizeY;
        windField_Result_Pong.volumeDepth = WindFieldSizeY;
        windField_Div_Pressure_Ping.volumeDepth = WindFieldSizeY;
        windField_Div_Pressure_Pong.volumeDepth = WindFieldSizeY;
        windField_Div_Pressure_Pong.volumeDepth = WindFieldSizeY;
        windField_SDF.volumeDepth = WindFieldSizeY;
        Test3D.volumeDepth = WindFieldSizeY;

        windField_Result_Ping.enableRandomWrite = true;
        windField_Result_Pong.enableRandomWrite = true;
        windField_Div_Pressure_Ping.enableRandomWrite = true;
        windField_Div_Pressure_Pong.enableRandomWrite = true;
        windField_SDF.enableRandomWrite = true;
        Test3D.enableRandomWrite = true;

        Test3D.filterMode = FilterMode.Bilinear;

        windField_Div_Pressure_Ping.Create();
        windField_Div_Pressure_Pong.Create();
        windField_Result_Pong.Create();
        windField_Result_Ping.Create();
        windField_SDF.Create();
        Test3D.Create();

    }
    /// <summary>
    /// 
    /// </summary>
    void InitialComputeShader()
    {
        Test2D = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
        Test2D.format = RenderTextureFormat.ARGBHalf;
        Test2D.enableRandomWrite = true;
        Test2D.Create();

        directionalMotorBuffer = new ComputeBuffer(MAXMOTOR, 28);
        omniMotorBuffer = new ComputeBuffer(MAXMOTOR, 20);
        vortexMotorBuffer = new ComputeBuffer(MAXMOTOR, 32);
        movingMotorBuffer = new ComputeBuffer(MAXMOTOR, 36);
        obstacleVertexBuffer = new ComputeBuffer(MAXVERTEX, 28);
        obstacleOBBBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAXOBSTACLE, 88);
        obstacle_OBB_PositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAXOBSTACLE, 12);
        obstacle_OBB_HalfExtentsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAXOBSTACLE, 12);
        obstacle_OBB_RotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAXOBSTACLE, 64);
    }

    void UpdateComputeShader()
    {

        /*
        wComputeShader.SetVector(emittorPos, windEmitter.GetPos());
        wComputeShader.SetVector(emittorDir, windEmitter.GetDir());
        wComputeShader.SetFloat(deltaTime, Time.deltaTime);
        wComputeShader.SetVector("MoveVelocity", windEmitter.GetSpeed());
        wComputeShader.SetBool("IsAdd", windEmitter.GetAdd());

        Vector3 translation = this.transform.position + new Vector3(WindFieldSizeX / 2.0f,WindFieldSizeY / 2.0f ,WindFieldSizeZ / 2.0f);
        Matrix4x4 WindSpaceMatrix = Matrix4x4.Translate(translation);
        Matrix4x4 InvWindSpaceMatrix = WindSpaceMatrix.inverse;
        wComputeShader.SetMatrix(wfSpaceMatrix, WindSpaceMatrix);
        wComputeShader.SetMatrix(wfSpaceMatrixInv, InvWindSpaceMatrix);
    }

    void UpdateDiffusionTex()
    {
        wComputeShader.SetTexture(kernelHandle_Diffusion, "Test", Test2D);
        wComputeShader.SetTexture(kernelHandle_Diffusion, Result_Ping, windField_Result_Ping);
        wComputeShader.SetTexture(kernelHandle_Diffusion, Result_Pong, windField_Result_Pong);
        wComputeShader.SetTexture(kernelHandle_Diffusion, "FinalResult", Test3D);
        */

    }


    void Swap()
    {
        string temp = Result_Ping;
        Result_Ping = Result_Pong;
        Result_Pong = temp;


    }

    /// <summary>
    /// 风力发动机 施加力代码
    /// </summary>
    void WindMotorAdd()
    {
        //更新风力位置
        UpdateWindMotor();


        wComputeShader_WindMotor.SetVector("WindFieldSize", new Vector3(WindFieldSizeX - 1, WindFieldSizeY - 1, WindFieldSizeZ - 1));
        cmd.BeginSample("Force");
        wComputeShader_WindMotor.SetFloat("VoxelSize", VoxelSize);
        Vector3 translation = this.transform.position * VoxelSize + new Vector3(WindFieldSizeX * VoxelSize / 2.0f, WindFieldSizeY * VoxelSize / 2.0f, WindFieldSizeZ * VoxelSize / 2.0f);
        Matrix4x4 WindSpaceMatrix = Matrix4x4.Translate(translation);
        Matrix4x4 InvWindSpaceMatrix = WindSpaceMatrix.inverse;
        wComputeShader_WindMotor.SetMatrix(wfSpaceMatrix, WindSpaceMatrix);
        wComputeShader_WindMotor.SetMatrix(wfSpaceMatrixInv, InvWindSpaceMatrix);
        wComputeShader_WindMotor.SetVector("WindFieldCenter", this.transform.position);

        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_WindMotor, kernel_In, Test3D);
        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_WindMotor, kernel_Out, windField_Result_Ping);
        cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_WindMotor, "Test", Test2D);


        cmd.DispatchCompute(wComputeShader_WindMotor, kernelHandle_WindMotor, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        //wComputeShader.Dispatch(kernelHandle_AddForce, WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);

        cmd.EndSample("Force");
    }
    /// <summary>
    ///  风的扩散项
    /// </summary>
    void Diffusion()
    {
        cmd.BeginSample("Diffusion");
        wComputeShader_Diffusion.SetFloat("VoxelSize", VoxelSize);
        wComputeShader_Diffusion.SetVector("WindFieldSize", new Vector3(WindFieldSizeX - 1, WindFieldSizeY - 1, WindFieldSizeZ - 1));
        wComputeShader_Diffusion.SetFloat("PopVelocity", PopVelocity);
        wComputeShader_Diffusion.SetFloat(deltaTime, Time.deltaTime);
        //ping
        cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Ping);
        cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
        cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


        //pong
        cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Pong);
        cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Ping);
        cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

        for (int i = 0; i < 3; i++)
        {
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Ping);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Ping);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

        }




        cmd.EndSample("Diffusion");

    }
    /// <summary>
    /// 风的对流项和平流项(对流项似乎是多余的..)
    /// </summary>
    void Advect()
    {
        cmd.BeginSample("Advect");
        wComputeShader_Advect.SetFloat("VoxelSize", VoxelSize);
        wComputeShader_Advect.SetVector("WindFieldSize", new Vector3(WindFieldSizeX - 1, WindFieldSizeY - 1, WindFieldSizeZ - 1));
        wComputeShader_Advect.SetFloat(deltaTime, Time.deltaTime);

        cmd.SetComputeTextureParam(wComputeShader_Advect, kernelHandle_Advect_Negative, kernel_In, windField_Result_Ping);
        cmd.SetComputeTextureParam(wComputeShader_Advect, kernelHandle_Advect_Negative, kernel_Out, windField_Result_Pong);
        cmd.DispatchCompute(wComputeShader_Advect, kernelHandle_Advect_Negative, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        cmd.EndSample("Advect");
        //wComputeShader.Dispatch(kernelHandle_Advect, WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);
    }
    /// <summary>
    /// 风的投影项(我不确定这个有没有用)
    /// </summary>
    void Project()
    {
        cmd.BeginSample("Project");
        wComputeShader_Project.SetFloat("VoxelSize", VoxelSize);
        wComputeShader_Project.SetVector("WindFieldSize", new Vector3(WindFieldSizeX - 1, WindFieldSizeY - 1, WindFieldSizeZ - 1));
        wComputeShader_Project.SetFloat(deltaTime, Time.deltaTime);
        cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_1, kernel_In, windField_Result_Pong);
        cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_1, Div_Pressure_Output, windField_Div_Pressure_Ping);
        cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_1, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        // wComputeShader.Dispatch(kernelHandle_Project_1, WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);

        for (int i = 0; i < 10; i++)
        {
            //这里导致了X轴扩散方向大于了Z轴，解决一下__1.24
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_2, Div_Pressure_Input, windField_Div_Pressure_Ping);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_2, Div_Pressure_Output, windField_Div_Pressure_Pong);
            cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_2, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            //wComputeShader.Dispatch(kernelHandle_Project_2,WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);

            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_2, Div_Pressure_Input, windField_Div_Pressure_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_2, Div_Pressure_Output, windField_Div_Pressure_Ping);
            cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_2, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        }
        cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, kernel_In, windField_Result_Pong);
        cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, Div_Pressure_Input, windField_Div_Pressure_Ping);
        cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, kernel_Out, Test3D);
        cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_3, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


        cmd.EndSample("Project");
    }

    void ObstacleDetective()
    {
        //这里可以创一个Clear也可以声明新的
        obstacleMeshList.Clear();
        obstacleOBBList.Clear();
        obstacle_OBB_RotationList.Clear();
        obstacle_OBB_HalfExtentsList.Clear();
        obstacle_OBB_PositionList.Clear();
        int num = 0;
        //windObstacleCount
        for (int i = 0; i < goTests.Count; i++)
        {
            // Mesh obstacle = windObstacles[i].mesh;

            Mesh obstacle = goTests[i].GetComponent<MeshFilter>().sharedMesh;
            Matrix4x4 localToWorld = goTests[i].transform.localToWorldMatrix;
            Matrix4x4 localToWorldNormal = (localToWorld.inverse).transpose;//法线的特殊旋转矩阵
            OBB obb = OBBCreate_Transform(goTests[i]);
            obstacleOBBList.Add(obb);
            obstacle_OBB_HalfExtentsList.Add(obb.HalfExtents);
            obstacle_OBB_RotationList.Add(obb.Rotation);
            obstacle_OBB_PositionList.Add(obb.Center);

            for (int j = 0; j < obstacle.vertexCount; j++)
            {
                Vertex meshData = new Vertex();
                //转世界坐标
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(obstacle.vertices[j]);
                meshData.Position = worldPos; //好像transform的顶点位置有点偏移？
                meshData.Normal = localToWorldNormal.MultiplyPoint3x4(obstacle.normals[j]);
                meshData.ObIndex = i;
                obstacleMeshList.Add(meshData);
                num++;
            }
        }

        //把OBB传给VFX
        obstacleOBBBuffer.SetData(obstacleOBBList.ToArray());
        //unity DE BUG 
        obstacle_OBB_RotationBuffer.SetData(obstacle_OBB_RotationList.ToArray());
        obstacle_OBB_HalfExtentsBuffer.SetData(obstacle_OBB_HalfExtentsList.ToArray());
        obstacle_OBB_PositionBuffer.SetData(obstacle_OBB_PositionList.ToArray());

        particleTest.SetGraphicsBuffer(Obstacle_OBB_HalfExtents_BufferId, obstacle_OBB_HalfExtentsBuffer);
        particleTest.SetGraphicsBuffer(Obstacle_OBB_Position_BufferId, obstacle_OBB_PositionBuffer);
        particleTest.SetGraphicsBuffer(Obstacle_OBB_Rotation_BufferId, obstacle_OBB_RotationBuffer);
        particleTest.SetInt("OBBCount", goTests.Count);
        //传给SDF生成的CS 用于生成SDF
        obstacleVertexBuffer.SetData(obstacleMeshList.ToArray());
        wComputeShader_Obstacle_SDF.SetFloat("VoxelSize", VoxelSize);
        wComputeShader_Obstacle_SDF.SetVector("WindFieldSize", new Vector3(WindFieldSizeX - 1, WindFieldSizeY - 1, WindFieldSizeZ - 1));
        wComputeShader_Obstacle_SDF.SetVector("WindFieldCenter", this.transform.position);
        wComputeShader_Obstacle_SDF.SetBuffer(kernelHandle_SDF_Create, Obstacle_Vertex_BufferId, obstacleVertexBuffer);
        wComputeShader_Obstacle_SDF.SetBuffer(kernelHandle_SDF_Create, Obstacle_OBB_BufferId, obstacleOBBBuffer);
        wComputeShader_Obstacle_SDF.SetInt(Obstacle_NumId, num);


        cmd.BeginSample("SDF_Create");
        cmd.SetComputeTextureParam(wComputeShader_Obstacle_SDF, kernelHandle_SDF_Create, kernel_Out, windField_SDF);
        cmd.DispatchCompute(wComputeShader_Obstacle_SDF, kernelHandle_SDF_Create, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

        particleTest.SetTexture("Obstacle_SDF", windField_SDF);

        /*for (int i = 1; StepSize / i != 1; i++)
        {
            int Step = StepSize / i;
            wComputeShader_Obstacle_SDF.SetInt("StepSize", Step);
            cmd.SetComputeTextureParam(wComputeShader_Obstacle_SDF,kernelHandle_SDF_Iterate,kernel_Out, windField_SDF);
            cmd.DispatchCompute(wComputeShader_Obstacle_SDF, kernelHandle_SDF_Iterate, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
        }*/
        cmd.EndSample("SDF_Create");
    }
    public void AddWindMotor(WindMotor motor)
    {
        windMotors.Add(motor);
    }
    public void RemoveWindMotor(WindMotor motor)
    {
        windMotors.Remove(motor);
    }

    void UpdateWindMotor()
    {
        List<DelayedOperation> motorOpration = new List<DelayedOperation>();

        directionalMotorList.Clear();
        omniMotorList.Clear();
        vortexMotorList.Clear();
        movingMotorList.Clear();

        directionalMotor_num = 0;
        omniMotor_num = 0;
        vortexMotor_num = 0;
        movingMotor_num = 0;

        foreach (var motor in windMotors)
        {
            motorOpration.Add(() =>
            {
                motor.UpdateWindMotor();
                switch (motor.MotorType)
                {
                    case MotorType.Directional:
                        directionalMotor_num++;
                        directionalMotorList.Add(motor.motorDirectional);
                        break;

                    case MotorType.Omni:
                        omniMotor_num++;
                        omniMotorList.Add(motor.motorOmni);
                        break;

                    case MotorType.Vortex:
                        vortexMotor_num++;
                        vortexMotorList.Add(motor.motorVortex);
                        break;

                    case MotorType.Moving:
                        movingMotor_num++;
                        movingMotorList.Add(motor.motorMoving);
                        break;
                }
            });
        }

        foreach (var operation in motorOpration)
        {
            operation.Invoke();
        }
        if (directionalMotor_num < MAXMOTOR)
        {
            MotorDirectional motor = WindMotor.GetEmptyMotorDirectional();
            for (int i = directionalMotor_num; i < MAXMOTOR; i++)
            {
                directionalMotorList.Add(motor);
            }
        }
        if (omniMotor_num < MAXMOTOR)
        {
            MotorOmni motor = WindMotor.GetEmptyMotorOmni();
            for (int i = omniMotor_num; i < MAXMOTOR; i++)
            {
                omniMotorList.Add(motor);
            }
        }
        if (vortexMotor_num < MAXMOTOR)
        {
            MotorVortex motor = WindMotor.GetEmptyMotorVortex();
            for (int i = vortexMotor_num; i < MAXMOTOR; i++)
            {
                vortexMotorList.Add(motor);
            }
        }
        if (movingMotor_num < MAXMOTOR)
        {
            MotorMoving motor = WindMotor.GetEmptyMotorMoving();
            for (int i = movingMotor_num; i < MAXMOTOR; i++)
            {
                movingMotorList.Add(motor);
            }
        }

        //Direcition
        directionalMotorBuffer.SetData(directionalMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Directional_BufferId, directionalMotorBuffer);
        //OmniMotor
        omniMotorBuffer.SetData(omniMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Omni_BufferId, omniMotorBuffer);
        //Vortex
        vortexMotorBuffer.SetData(vortexMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Vortex_BufferId, vortexMotorBuffer);
        //Moving
        movingMotorBuffer.SetData(movingMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Moving_BufferId, movingMotorBuffer);

        wComputeShader_WindMotor.SetInt(Directional_NumId, directionalMotor_num);
        wComputeShader_WindMotor.SetInt(Omni_NumId, omniMotor_num);
        wComputeShader_WindMotor.SetInt(Vortex_NumId, vortexMotor_num);
        wComputeShader_WindMotor.SetInt(Moving_NumId, movingMotor_num);
    }

    /// <summary>
    /// OBB的创建(使用Transform)
    /// </summary>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    OBB OBBCreate_Transform(GameObject gameObject)
    {
        OBB oBB = new OBB();

        oBB.Center = gameObject.transform.position;
        oBB.Rotation = Matrix4x4.Rotate(gameObject.transform.rotation);
        oBB.HalfExtents = gameObject.transform.localScale / 2;

        Vector3 center = oBB.Center;
        Vector3 obbAxisX = Matrix4x4.Rotate(gameObject.transform.rotation).GetColumn(0);
        Vector3 obbAxisY = Matrix4x4.Rotate(gameObject.transform.rotation).GetColumn(1);
        Vector3 obbAxisZ = Matrix4x4.Rotate(gameObject.transform.rotation).GetColumn(2);

        Vector3 halfExtents = oBB.HalfExtents;

        Debug.DrawLine(center, center + obbAxisX * halfExtents.x, Color.red);   // X 轴
        Debug.DrawLine(center, center + obbAxisY * halfExtents.y, Color.green); // Y 轴
        Debug.DrawLine(center, center + obbAxisZ * halfExtents.z, Color.blue);  // Z 轴

        return oBB;
    }

    /// <summary>
    /// OBB的创建(使用顶点)
    /// </summary>
    /// <param name="gameObject"></param>
    /// <returns></returns>
    OBB OBBCreate_Vertexs(GameObject gameObject)
    {
        List<Vector3> vertexs = new List<Vector3>();
        Matrix4x4 localToWorld = gameObject.transform.localToWorldMatrix;
        OBB obb = new OBB();
        Mesh goMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

        goMesh.GetVertices(vertexs);
        //oBB的中心点
        Vector3 center = Vector3.zero;
        foreach (var v in goMesh.vertices)
        {
            center += localToWorld.MultiplyPoint(v);
        }
        center /= goMesh.vertexCount;

        /* //计算协方差矩阵(顺便归一)
         Matrix4x4 cov = Matrix4x4.zero;
         foreach (var v in goMesh.vertices)
         {
             Vector3 delta = localToWorld.MultiplyPoint(v) - center;
             cov.m00 += delta.x * delta.x / goMesh.vertexCount;
             cov.m01 += delta.x * delta.y / goMesh.vertexCount;
             cov.m02 += delta.x * delta.z / goMesh.vertexCount;
             cov.m10 += delta.y * delta.x / goMesh.vertexCount;
             cov.m11 += delta.y * delta.y / goMesh.vertexCount;
             cov.m12 += delta.y * delta.z / goMesh.vertexCount;
             cov.m20 += delta.z * delta.x / goMesh.vertexCount;
             cov.m21 += delta.z * delta.y / goMesh.vertexCount;
             cov.m22 += delta.z * delta.z / goMesh.vertexCount;
         }*/

        float4x4 cov = CalateCov(gameObject);

        // 计算特征值和特征向量（使用雅可比迭代法）
        float4x4 eigenvalues;
        float4x4 eigenvectors;
        eigenvectors = EigenMatrix.JacobiMatrixs(cov);
        eigenvalues = EigenMatrix.PT_A_P(cov, eigenvectors);//计算出来的是特征值

        // 确保特征向量是单位向量
        Vector3 eigenVec1 = eigenvectors.c0.xyz;
        Vector3 eigenVec2 = eigenvectors.c1.xyz;
        Vector3 eigenVec3 = eigenvectors.c2.xyz;

        // 构建旋转矩阵
        Matrix4x4 rotation = Matrix4x4.identity;
        rotation.SetColumn(0, new Vector4(eigenVec1.x, eigenVec1.y, eigenVec1.z, 0).normalized);
        rotation.SetColumn(1, new Vector4(eigenVec2.x, eigenVec2.y, eigenVec2.z, 0).normalized);
        rotation.SetColumn(2, new Vector4(eigenVec3.x, eigenVec3.y, eigenVec3.z, 0).normalized);
        rotation.SetColumn(3, new Vector4(0, 0, 0, 1));

        //计算半轴
        Vector3 halfExtents = Vector3.zero;
        foreach (var v in goMesh.vertices)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint(v);
            Vector3 localPos = new Vector3(
               Vector3.Dot(worldPos - center, eigenVec1),
               Vector3.Dot(worldPos - center, eigenVec2),
               Vector3.Dot(worldPos - center, eigenVec3)
           );
            halfExtents.x = Mathf.Max(halfExtents.x, Mathf.Abs(localPos.x));
            halfExtents.y = Mathf.Max(halfExtents.y, Mathf.Abs(localPos.y));
            halfExtents.z = Mathf.Max(halfExtents.z, Mathf.Abs(localPos.z));
        }

        obb.Center = center;
        obb.HalfExtents = halfExtents;
        obb.Rotation = rotation.inverse;

        // OBB 的轴就是特征向量
        Vector3 obbAxisX = eigenvectors.c0.xyz;
        Vector3 obbAxisY = eigenvectors.c1.xyz;
        Vector3 obbAxisZ = eigenvectors.c2.xyz;

        Debug.DrawLine(center, center + obbAxisX * halfExtents.x, Color.red);   // X 轴
        Debug.DrawLine(center, center + obbAxisY * halfExtents.y, Color.green); // Y 轴
        Debug.DrawLine(center, center + obbAxisZ * halfExtents.z, Color.blue);  // Z 轴

        return obb;
    }

    /// <summary>
    /// 求均值
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    static float EX(List<float> x)
    {
        float a = 0;
        for (int i = 0; i < x.Count; i++)
        {
            a += x[i];
        }

        return a / x.Count;
    }
    /// <summary>
    /// 求X和Y的均值
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    static float EXY(List<float> x, List<float> y)
    {
        int count = Mathf.Min(x.Count, y.Count);
        float s = 0;
        for (int i = 0; i < count; i++)
        {
            s += x[i] * y[i];
        }

        return s / count;
    }
    /// <summary>
    /// 求协方差
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    static float COV(List<float> x, List<float> y)
    {
        float ex = EX(x);
        float ey = EX(y);
        float exy = EXY(x, y);
        return exy - ex * ey;
    }

    /// <summary>
    /// 求协方差矩阵 协方差矩阵是实对称矩阵 可以用Jacobi计算特征值和特征向量
    /// </summary>
    float4x4 CalateCov(GameObject GO)
    {
        float4x4 Cov = new float4x4();      
        Mesh mesh = GO.GetComponent<MeshFilter>().sharedMesh;
        Matrix4x4 localToWorld = GO.transform.localToWorldMatrix;
        
        List<float> x = new List<float>();
        List<float> y = new List<float>();
        List<float> z = new List<float>();
        foreach (var v in mesh.vertices)
        {
            float3 wv = localToWorld.MultiplyPoint3x4(v);
            x.Add(wv.x);
            y.Add(wv.y);
            z.Add(wv.z);
        }

        float covXX = COV(x, x);
        float covXY = COV(x, y);
        float covXZ = COV(x, z);

        float covYY = COV(y, y);
        float covYZ = COV(z, y);
        float covZZ = COV(z, z);
        //协方差矩阵  实对称矩阵  各个特征向量都是垂直的  它必可相似对角化，且相似对角阵上的元素即为矩阵本身特征值
        Cov.c0 = new float4(covXX, covXY, covXZ, 0);
        Cov.c1 = new float4(covXY, covYY, covYZ, 0);
        Cov.c2 = new float4(covXZ, covYZ, covZZ, 0);
        Cov.c3 = new float4(0, 0, 0, 1);//打酱油

        return Cov;
    }


}

