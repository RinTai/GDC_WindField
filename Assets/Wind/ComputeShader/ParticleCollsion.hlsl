#ifndef PATICLE_COLLSION
#define PATICLE_COLLSION

// 定义OBB结构
struct OBB
{
    float3 center;
    float3 halfExtents;
    float4x4 rotation;
};

//为什么unity不内置一个Inverse..
float4x4 inverse(float4x4 m)
{
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
    float idet = 1.0f / det;

    float4x4 ret;

    ret[0][0] = t11 * idet;
    ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
    ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
    ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

    ret[1][0] = t12 * idet;
    ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
    ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
    ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

    ret[2][0] = t13 * idet;
    ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
    ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
    ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

    ret[3][0] = t14 * idet;
    ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
    ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
    ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

    return ret;
}


bool BounceCompute(float4x4 rotation, float3 halfExtents, float3 center, float3 velocity,float3 worldPos, float3 lastWorldPosition, inout float3 worldNormal)
{
    float4x4 transposeRotation = inverse(rotation);
    
    float3 pLocal = mul(transposeRotation, float4(worldPos - center,1.0)).xyz;
    
    float3 pLocalLast = mul(transposeRotation, float4(lastWorldPosition - center, 1.0)).xyz;
    
    float3 vLocal = mul(transposeRotation, float4(velocity, 1.0));
    
    float3 Axia = float3(0, 0, 0);
    
    if (pLocal.x < -halfExtents.x || pLocal.x > halfExtents.x)
    {
        return false;
    }
    if (pLocal.y < -halfExtents.y || pLocal.y > halfExtents.y)
    {
        return false;
    }
    if (pLocal.z < -halfExtents.z || pLocal.z > halfExtents.z)
    {
        return false;
    }
    
    float tmin = 65536;
    
    //计算离正向的X边界的时间 来判断边界是谁 P ->  正，N -> 负
    float tP_x = abs((pLocalLast.x - halfExtents.x) / vLocal.x);
    if (tP_x < tmin)
    {
        tmin = tP_x;
        Axia = float3(1, 0, 0);
    }
    float tN_x = abs((pLocalLast.x + halfExtents.x) / vLocal.x);
    if (tN_x < tmin)
    {
        tmin = tN_x;
        Axia = float3(-1, 0, 0);
    }
    
    
    float tP_y = abs((pLocalLast.y - halfExtents.y) / vLocal.y);
    if (tP_y < tmin)
    {
        tmin = tP_y;
        Axia = float3(0, 1, 0);
    }
    float tN_y = abs((pLocalLast.y + halfExtents.y) / vLocal.y);
    if (tN_y < tmin)
    {
        tmin = tN_y;
        Axia = float3(0, -1, 0);
    }
    
    float tP_z = abs((pLocalLast.z - halfExtents.z) / vLocal.z);
    if (tP_z < tmin)
    {
        tmin = tP_z;
        Axia = float3(0, 0, 1);
    }
    float tN_z = abs((pLocalLast.z + halfExtents.z) / vLocal.z);
    if (tN_z < tmin)
    {
        tmin = tN_z;
        Axia = float3(0, 0, -1);
    }
   //把Axia转世界去
    float4x4 InvTransRotation = inverse(transposeRotation);
    worldNormal = mul(rotation, float4(Axia, 1.0)).xyz;
    return true;
}
void CustomHLSL(inout VFXAttributes attributes,in float ParticleForce, in float3 WorldPos,in float4 SDFvalue, in float Friction, in float3 WindForce, in float deltaTime, in StructuredBuffer<float4x4> Obstacle_OBB_Rotation, in StructuredBuffer<float3> Obstacle_OBB_Position, in StructuredBuffer<float3> Obstacle_OBB_HalfExtents, in int OBBCount)
{
    bool isBounce = false;
    float3 normal = float3(0, 0, 0);
    float3 velocity = attributes.velocity;
    for (int i = 0; i < OBBCount ;i++)
    {
        float4x4 rotation = Obstacle_OBB_Rotation[i];
        float3 center = Obstacle_OBB_Position[i];
        float3 halfExtents = Obstacle_OBB_HalfExtents[i];
        float3 lastWorldPos = WorldPos - deltaTime * attributes.velocity;
        isBounce = isBounce || BounceCompute(rotation, halfExtents, center,velocity , WorldPos, lastWorldPos, normal);
    }
    if (isBounce)
    {
        attributes.position = attributes.position +0.7f *  normal;
        double3 v_Projected = mul(dot(attributes.velocity, normal), normal);
        attributes.velocity = attributes.velocity - mul((1 + Friction) , v_Projected);
    }

    if(!isBounce)
    {
        
        float velocityDifference = length(attributes.velocity) - length(WindForce);
        if (velocityDifference >= 10.0f )
        {
            float3 windDirection = normalize(WindForce);
            float speed = length(attributes.velocity);
            float windSpeed = length(WindForce);
            float test = lerp(speed, windSpeed, 0.5f);
            attributes.velocity = windDirection * test;

        }
        else
        {
            attributes.velocity += ParticleForce * WindForce * deltaTime;
        }
    }
        //速度的增加
 
    
    // SDF碰撞
    /*
    if(SDFvalue.w < 0)
    {
        attributes.position = attributes.position + SDFvalue.xyz * abs(SDFvalue.w);
        float3 v_Projected = dot(attributes.velocity, SDFvalue.xyz) * SDFvalue.xyz;
        attributes.velocity = attributes.velocity - (1 + Friction) * v_Projected + WindForce * deltaTime;
    }*/

    

    }


#endif