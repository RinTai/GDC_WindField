using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.VFX;

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
        public Vector3 position;
        public Vector3 normal;
        public int ObIndex;
    }
    private RenderTexture windField_SDF;//用于记录场内的障碍物的SDF xyz是法线 W是距离
    private List<MeshFilter> windObstacles = new List<MeshFilter>(16);
    private List<Vertex> obstacleMeshList = new List<Vertex>();
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
    private ComputeBuffer obstacleSDFBuffer;

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
        Obstacle_BufferId = "VertexBuffer",
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
        debugParticle.SetVector3("_WindCenterPos",this.transform.position);

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
        windField_Div_Pressure_Pong = new RenderTexture(WindFieldSizeX,WindFieldSizeZ, 0);
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
        obstacleSDFBuffer = new ComputeBuffer(MAXVERTEX, 28);
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


        wComputeShader_WindMotor.SetVector("WindFieldSize", new Vector3(WindFieldSizeX, WindFieldSizeY, WindFieldSizeZ));
        cmd.BeginSample("Force");
        wComputeShader_WindMotor.SetFloat("VoxelSize", VoxelSize);
        Vector3 translation = this.transform.position * VoxelSize+ new Vector3(WindFieldSizeX * VoxelSize/ 2.0f, WindFieldSizeY  * VoxelSize/ 2.0f, WindFieldSizeZ * VoxelSize / 2.0f);
        Matrix4x4 WindSpaceMatrix = Matrix4x4.Translate(translation);
        Matrix4x4 InvWindSpaceMatrix = WindSpaceMatrix.inverse;
        wComputeShader_WindMotor.SetMatrix(wfSpaceMatrix, WindSpaceMatrix);
        wComputeShader_WindMotor.SetMatrix(wfSpaceMatrixInv, InvWindSpaceMatrix);
        wComputeShader_WindMotor.SetVector("WindFieldCenter",this.transform.position);

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
        wComputeShader_Diffusion.SetVector("WindFieldSize", new Vector3(WindFieldSizeX, WindFieldSizeY, WindFieldSizeZ));
        wComputeShader_Diffusion.SetFloat("PopVelocity",   PopVelocity);
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
        wComputeShader_Advect.SetVector("WindFieldSize", new Vector3(WindFieldSizeX, WindFieldSizeY, WindFieldSizeZ));
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
        wComputeShader_Project.SetVector("WindFieldSize", new Vector3(WindFieldSizeX, WindFieldSizeY, WindFieldSizeZ));
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
        int num = 0;
        //windObstacleCount
        for (int i = 0; i < goTests.Count; i++)
        {
            // Mesh obstacle = windObstacles[i].mesh;
            Mesh obstacle = goTests[i].GetComponent<MeshFilter>().sharedMesh;
            Matrix4x4 localToWorld = goTests[i].transform.localToWorldMatrix;
            for (int j = 0; j < obstacle.vertexCount; j++)
            {
                Vertex meshData = new Vertex();
                //转世界坐标
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(obstacle.vertices[j]);
                meshData.position = worldPos + new Vector3(-0.5f, -0.5f, -0.5f); //好像transform的顶点位置有点偏移？
                meshData.normal = obstacle.normals[j];
                meshData.ObIndex = i;
                obstacleMeshList.Add(meshData);
                num++;
            }
        }

    
       
        obstacleSDFBuffer.SetData(obstacleMeshList.ToArray());
        wComputeShader_Obstacle_SDF.SetFloat("VoxelSize", VoxelSize);
        wComputeShader_Obstacle_SDF.SetVector("WindFieldSize", new Vector3(WindFieldSizeX, WindFieldSizeY, WindFieldSizeZ));
        wComputeShader_Obstacle_SDF.SetVector("WindFieldCenter", this.transform.position);
        wComputeShader_Obstacle_SDF.SetBuffer(kernelHandle_SDF_Create, Obstacle_BufferId,obstacleSDFBuffer);
        wComputeShader_Obstacle_SDF.SetInt(Obstacle_NumId, num);


        cmd.BeginSample("SDF_Create");
        cmd.SetComputeTextureParam(wComputeShader_Obstacle_SDF, kernelHandle_SDF_Create, kernel_Out, windField_SDF);
        cmd.DispatchCompute(wComputeShader_Obstacle_SDF, kernelHandle_SDF_Create, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

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
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor,Directional_BufferId, directionalMotorBuffer);
        //OmniMotor
        omniMotorBuffer.SetData(omniMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Omni_BufferId, omniMotorBuffer);
        //Vortex
        vortexMotorBuffer.SetData(vortexMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor, Vortex_BufferId, vortexMotorBuffer);
        //Moving
        movingMotorBuffer.SetData(movingMotorList);
        wComputeShader_WindMotor.SetBuffer(kernelHandle_WindMotor,Moving_BufferId, movingMotorBuffer);

        wComputeShader_WindMotor.SetInt(Directional_NumId, directionalMotor_num);
        wComputeShader_WindMotor.SetInt(Omni_NumId, omniMotor_num);
        wComputeShader_WindMotor.SetInt(Vortex_NumId, vortexMotor_num);
        wComputeShader_WindMotor.SetInt(Moving_NumId, movingMotor_num);
    }

    private void OnTriggerStay(Collider other)
    {
        GameObject ob = other.gameObject;
        MeshFilter meshFilter = ob.GetComponent<MeshFilter>();
        if (windObstacles.Contains(meshFilter) == false && ob.tag == "Obstacle")
        {
            windObstacles.Add(meshFilter);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        GameObject ob = other.gameObject;
        MeshFilter meshFilter = ob.GetComponent<MeshFilter>();
        if (windObstacles.Contains(meshFilter) == true)
        {
            windObstacles.Add(meshFilter);
        }
    }
}

