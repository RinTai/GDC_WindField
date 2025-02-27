#ifndef PATICLE_COLLSION
#define PATICLE_COLLSION

//Ðý×ª¾ØÕó ÖÐÐÄ °ëÖá
Texture3D Obstacle_SDF;

/*bool BounceCompute(OBB obb, float3 position)
{
    float4x4 transposeRotation = (obb.rotation);
    
    float3 pLocal = mul(transposeRotation, (position - obb.center));
    
    float3 halfExtents = obb.halfExtents;
    
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
    
    return true;
}*/
void CustomHLSL(inout VFXAttributes attributes, in float3 worldPos,in float4 SDFvalue,in float Friction)
{
    if(SDFvalue.w <= 0.5f)
    {
        attributes.position = attributes.position + (0.5f + abs(SDFvalue.w)) * SDFvalue.xyz;
        float3 v_Projected = dot(attributes.velocity, SDFvalue.xyz) * SDFvalue.xyz;
        attributes.velocity = attributes.velocity - (1 + Friction) * v_Projected;
    }
 
}


#endif