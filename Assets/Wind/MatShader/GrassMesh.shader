Shader "Custom/GrassMesh"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HideInInspector]
        _WindField("WindField",3D) = "white"{}
        _VoxelSize("VoxelSize",Float) = 1.0
        _ForceStrength("ForceStrength",Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        
        Pass
        {            
    
        HLSLPROGRAM
        #pragma vertex vert 
        #pragma fragment frag
        #pragma target 5.0
        #pragma enable_d3d11_debug_symbols
        #include "UnityCG.cginc"
        #include "HLSLSupport.cginc"


        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        float4 _Color;
        sampler3D _WindField;
        float3 WindFieldCenter;
        float VoxelSize;
        float _ForceStrength;

        float3 WindFieldOffset;

        struct appdata
        {
            float4 vertex : POSITION;
            float3 texcoord : TEXCOORD0;
            float4 normal : NORMAL;
        };

        struct v2f
        {
            float4 Pos : SV_POSITION;
            float3 texcoord : TEXCOORD0;
            float4 normal : NORMAL;
        };

        v2f vert(appdata input)
        {
            v2f output;
            float4 wPos = mul(unity_ObjectToWorld,input.vertex)  ;
            float4 sourcePos = mul(unity_ObjectToWorld,float4(0,0,0,1));
            float4 id = ((sourcePos + float4(128,0,128,0)) - float4(WindFieldCenter,0.0f)) ;
            id = float4(float(id.x) / 256.0f,float(id.z) / 256.0f ,float(id.y) / 16.0f,0.0);
            float3 velocityOffset = float3(0,0,0);
            velocityOffset =   tex3Dlod(_WindField,float4(id.xyz ,0)) + 0.00001f;

            //Àë¸ùµÄ¾àÀë 
            float3 toRoot = float3(input.vertex.xyz - float4(0,0,0));
            float disToRoot = length(toRoot);
            float4 mPos = wPos ;

            output.normal = mul(unity_ObjectToWorld,input.normal);                              
            output.Pos = mul(UNITY_MATRIX_VP,mPos);
            

            output.texcoord = tex3Dlod(_WindField,float4(id.xyz,0));

            return output;
        }

            float4 frag(v2f input) : SV_TARGET
            {
                float diffuse = (dot(input.normal,float3(0,-1,0)) + 1) / 2;
                return float4(0,diffuse,0,1);
            }

        ENDHLSL
        }
    }
    FallBack "Diffuse"
}
