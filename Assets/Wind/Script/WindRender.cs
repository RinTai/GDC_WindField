using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

//这一套是加入了RenderFeature的WindField 应该性能好一点吧..
public class WindRender : ScriptableRendererFeature
{

    [System.Serializable]
    public struct Settings 
    {
        public ComputeShader wComputeShader_WindMotor; //CS_1 
        public ComputeShader wComputeShader_Diffusion;
        public ComputeShader wComputeShader_Advect;
        public ComputeShader wComputeShader_Project;
        public Material test;
        public VisualEffect particleTest;
    }

    public Settings wSettings;
    [HideInInspector]
    public static WindFieldPass m_WindFieldPass;


    public class WindFieldPass : ScriptableRenderPass
    {
        private static string
        kernel_In = "InputResult",
        kernel_Out = "OutputResult",
        kernel_AddForce = "CSAddForce",
        kernel_Diffusion_1 = "CSDiffusion",
        kernel_Advect_Negative = "CSAdvect_Negative",
        kernel_Project_1 = "CSProj_1",
        kernel_Project_2 = "CSProj_2",
        kernel_Project_3 = "CSProj_3",
        kernel_Test = "CSFinal",
        deltaTime = "deltaTime",
        wfSpaceMatrix = "WindSpaceMatrix",
        wfSpaceMatrixInv = "InvWindSpaceMatrix",
        div_Pressure = "Div_Pressure",
        Directional_BufferId = "DirectionalMotorBuffer",
        Directional_NumId = "DirectionalMotorBufferCount",
        Omni_BufferId = "OmniMotorBuffer",
        Omni_NumId = "OmniMotorBufferCount",
        Vortex_BufferId = "VortexMotorBuffer",
        Vortex_NumId = "VortexMotorBufferCount",
        Moving_BufferId = "MovingMotorBuffer",
        Moving_NumId = "MovingMotorBufferCount";

        private static int WindFieldSizeX = 256;
        private static int WindFieldSizeY = 16;
        private static int WindFieldSizeZ = 256;
        private static int MAXMOTOR = 10;
        private static float VoxelSize = 1f;

        public ComputeShader wComputeShader_WindMotor; //CS_1 
        public ComputeShader wComputeShader_Diffusion;
        public ComputeShader wComputeShader_Advect;
        public ComputeShader wComputeShader_Project;

        private VisualEffect particleTest;

        public Vector4 velocity;
        public float pressure;
        public Vector3 padding;

 
        private const string bufferName = "WindFieldBuffer";
        CommandBuffer cmd;

        private int kernelHandle_WindMotor;
        private int kernelHandle_Diffusion;//内核句柄
        private int kernelHandle_Advect_Negative;
        private int kernelHandle_Project_1;
        private int kernelHandle_Project_2;
        private int kernelHandle_Project_3;
        private int kernelHandle_Test;

        private RenderTexture Test2D;
        //风场的数组，在这里用来测试吧
        private RenderTexture Test3D;
        private RenderTexture windField_Result_Ping;
        private RenderTexture windField_Result_Pong;
        private RenderTexture windField_Div_Pressure;

        private delegate void DelayedOperation(); //延迟触发用的委托

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

        private Material testMat;
        public Texture2D windTexture;


        private string
            Result_Ping = "Result_Ping",
            Result_Pong = "Result_Pong";


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

        /// <summary>
        /// 初始化各项风场纹理
        /// </summary>
        void InitialField()
        {
            windField_Result_Ping = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
            windField_Result_Pong = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
            windField_Div_Pressure = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);
            Test3D = new RenderTexture(WindFieldSizeX, WindFieldSizeZ, 0);

            windField_Result_Ping.name = "Result_Ping";
            windField_Result_Pong.name = "Result_Pong";
            windField_Div_Pressure.name = "Div_Pressure";
            Test3D.name = "FinalResult";

            windField_Result_Ping.dimension = TextureDimension.Tex3D;
            windField_Result_Pong.dimension = TextureDimension.Tex3D;
            windField_Div_Pressure.dimension = TextureDimension.Tex3D;
            Test3D.dimension = TextureDimension.Tex3D;

            windField_Result_Ping.format = RenderTextureFormat.ARGBHalf;
            windField_Result_Pong.format = RenderTextureFormat.ARGBHalf;
            windField_Div_Pressure.format = RenderTextureFormat.ARGBHalf;
            Test3D.format = RenderTextureFormat.ARGBHalf;

            windField_Result_Ping.volumeDepth = WindFieldSizeY;
            windField_Result_Pong.volumeDepth = WindFieldSizeY;
            windField_Div_Pressure.volumeDepth = WindFieldSizeY;
            Test3D.volumeDepth = WindFieldSizeY;

            windField_Result_Ping.enableRandomWrite = true;
            windField_Result_Pong.enableRandomWrite = true;
            windField_Div_Pressure.enableRandomWrite = true;
            Test3D.enableRandomWrite = true;

            windField_Div_Pressure.Create();
            windField_Result_Pong.Create();
            windField_Result_Ping.Create();
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
            UpdateWindMotor();

            cmd.BeginSample("Force");
            wComputeShader_WindMotor.SetFloat("VoxelSize", VoxelSize);
            Vector3 translation =  new Vector3(WindFieldSizeX * VoxelSize / 2.0f, WindFieldSizeY * VoxelSize/ 2.0f, WindFieldSizeZ * VoxelSize / 2.0f);
            Matrix4x4 WindSpaceMatrix = Matrix4x4.Translate(translation);
            Matrix4x4 InvWindSpaceMatrix = WindSpaceMatrix.inverse;
            wComputeShader_WindMotor.SetMatrix(wfSpaceMatrix, WindSpaceMatrix);
            wComputeShader_WindMotor.SetMatrix(wfSpaceMatrixInv, InvWindSpaceMatrix);

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

            wComputeShader_Diffusion.SetFloat("PopVelocity", 20f);
            wComputeShader_Diffusion.SetFloat(deltaTime, Time.deltaTime);
            //ping
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Ping);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


            //pong
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Ping);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

            for (int i = 0; i < 2; i++)
            {
                cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Ping);
                cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
                cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


                cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Pong);
                cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Ping);
                cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);

            }

            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Ping);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);


            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_In, windField_Result_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Diffusion, kernelHandle_Diffusion, kernel_Out, windField_Result_Pong);
            cmd.DispatchCompute(wComputeShader_Diffusion, kernelHandle_Diffusion, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            cmd.EndSample("Diffusion");

        }
        /// <summary>
        /// 风的对流项和平流项(对流项似乎是多余的..)
        /// </summary>
        void Advect()
        {
            cmd.BeginSample("Advect");
            wComputeShader_Advect.SetFloat(deltaTime, Time.deltaTime);


            cmd.SetComputeTextureParam(wComputeShader_Advect, kernelHandle_Advect_Negative, kernel_In, windField_Result_Ping);
            cmd.SetComputeTextureParam(wComputeShader_Advect, kernelHandle_Advect_Negative, kernel_Out, windField_Result_Pong);
            cmd.DispatchCompute(wComputeShader_Advect, kernelHandle_Advect_Negative, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            cmd.EndSample("Advect");
            //wComputeShader.Dispatch(kernelHandle_Advect, WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);
        }
        /// <summary>
        /// 风的投影项目(我不确定这个有没有用)
        /// </summary>
        void Project()
        {
            cmd.BeginSample("Project");
            wComputeShader_Project.SetFloat(deltaTime, Time.deltaTime);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_1, kernel_In, windField_Result_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_1, div_Pressure, windField_Div_Pressure);
            cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_1, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            // wComputeShader.Dispatch(kernelHandle_Project_1, WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);

            for (int i = 0; i < 10; i++)
            {
                //这里导致了X轴扩散方向大于了Z轴，解决一下__1.24
                cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_2, div_Pressure, windField_Div_Pressure);
                cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_2, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
                //wComputeShader.Dispatch(kernelHandle_Project_2,WindFieldSizeX / 8, WindFieldSizeY / 8, WindFieldSizeZ / 8);
            }
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, kernel_In, windField_Result_Pong);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, div_Pressure, windField_Div_Pressure);
            cmd.SetComputeTextureParam(wComputeShader_Project, kernelHandle_Project_3, kernel_Out, Test3D);
            cmd.DispatchCompute(wComputeShader_Project, kernelHandle_Project_3, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            cmd.EndSample("Project");
        }

        public void AddWindMotor(WindMotor motor)
        {
            windMotors.Add(motor);
            //UpdateWindMotor();
        }
        public void RemoveWindMotor(WindMotor motor)
        {
            windMotors.Remove(motor);
            //UpdateWindMotor();
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

            //对motor操作的延迟触发，foreach的特殊性不太允许边遍历边执行
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
        public WindFieldPass(Settings Input)
        {
            cmd = new CommandBuffer();
            cmd.name = bufferName;
            Application.targetFrameRate = 60;

            wComputeShader_Advect = Input.wComputeShader_Advect;
            wComputeShader_Diffusion = Input.wComputeShader_Diffusion;
            wComputeShader_Project = Input.wComputeShader_Project;
            wComputeShader_WindMotor = Input.wComputeShader_WindMotor;

            testMat = Input.test;
            particleTest = Input.particleTest;

            kernelHandle_WindMotor = wComputeShader_WindMotor.FindKernel(kernel_AddForce);
            kernelHandle_Diffusion = wComputeShader_Diffusion.FindKernel(kernel_Diffusion_1);
            kernelHandle_Advect_Negative = wComputeShader_Advect.FindKernel(kernel_Advect_Negative);
            kernelHandle_Project_1 = wComputeShader_Project.FindKernel(kernel_Project_1);
            kernelHandle_Project_2 = wComputeShader_Project.FindKernel(kernel_Project_2);
            kernelHandle_Project_3 = wComputeShader_Project.FindKernel(kernel_Project_3);
            kernelHandle_Test = wComputeShader_WindMotor.FindKernel(kernel_Test);

            InitialField();
            InitialComputeShader();

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear(); ;
             

        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {


        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {      
            WindMotorAdd();
            Diffusion();
            Advect();
            Project();

            cmd.BeginSample("Test");
            //Test
            cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_Test, kernel_In, Test3D);
            cmd.SetComputeTextureParam(wComputeShader_WindMotor, kernelHandle_Test, "Test", Test2D);
            cmd.DispatchCompute(wComputeShader_WindMotor, kernelHandle_Test, WindFieldSizeX / 8, WindFieldSizeZ / 8, WindFieldSizeY / 8);
            cmd.EndSample("Test");

            //test.SetTexture("_WindField", Test3D);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            testMat.SetTexture("_WindField", Test3D);
            Shader.SetGlobalTexture("_WindTexture", Test3D);
            //particleTest.SetTexture("_WindTexture",Test3D);
           

            //这里写纹理去获取，用来Debug看看呢
            windTexture = new Texture2D(Test2D.width, Test2D.height, TextureFormat.RGBAFloat, false);

            RenderTexture.active = Test2D;
            windTexture.ReadPixels(new Rect(0, 0, Test2D.width, Test2D.height), 0, 0);
            windTexture.Apply();
            RenderTexture.active = null;
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }


    public override void Create()
    {
        m_WindFieldPass = new WindFieldPass(wSettings);
        m_WindFieldPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

      
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_WindFieldPass);
    }
}


