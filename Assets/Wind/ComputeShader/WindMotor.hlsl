#ifndef WIND_MOTOR
#define WIND_MOTOR


struct MotorDirectional
{
    float3 position;
    float radiusSq;
    float3 force;
};
struct MotorOmni
{
    float3 position;
    float radiusSq;
    float force;
};
struct MotorVortex
{
    float3 position;
    float3 axis;
    float radiusSq;
    float force;
};
struct MotorMoving
{
    float3 prePosition;
    float moveLen;
    float3 moveDir;
    float radiusSq;
    float force;
};

//可能报错？基本只有这个部分会与外部交互，所以交互部分看这里就行了
StructuredBuffer<MotorDirectional> DirectionalMotorBuffer;
StructuredBuffer<MotorOmni> OmniMotorBuffer;
StructuredBuffer<MotorVortex> VortexMotorBuffer;
StructuredBuffer<MotorMoving> MovingMotorBuffer;

uniform int DirectionalMotorBufferCount;
uniform int OmniMotorBufferCount;
uniform int VortexMotorBufferCount;
uniform int MovingMotorBufferCount;

float LengthSq(float3 dir)
{
    return dot(dir, dir);
}

float DistanceSq(float3 pos1, float3 pos2)
{
    float3 dir = pos1 - pos2;
    return LengthSq(dir);
}
//方向风
void ApplyMotorDirectional(float voxelSize ,float3 cellPosWS, MotorDirectional motor, inout float3 velocityWS)
{
    float distanceSq = DistanceSq(cellPosWS, motor.position);
    if (distanceSq < motor.radiusSq)
    {
        velocityWS += motor.force;
    }
}
//点风源
void ApplyMotorOmni(float voxelSize, float3 cellPosWS, MotorOmni motor, inout float3 velocityWS)
{
    float3 dir = cellPosWS - motor.position ;
    if (length(dir) == 0 )
    {
        return;
    }
        float distanceSq = LengthSq(dir);
        if (distanceSq < motor.radiusSq)
        {
            velocityWS += normalize(dir) * motor.force * min(rsqrt(distanceSq), 5);
        }
    }
//聚风源
void ApplyMotorVortex(float voxelSize, float3 cellPosWS, MotorVortex motor, inout float3 velocityWS)
{
    float3 dir = cellPosWS - motor.position ;
    if(length(dir) == 0)
    {
        return;
    }
    float distanceSq = LengthSq(dir);
    if (distanceSq < motor.radiusSq)
    {
        velocityWS += motor.force * cross(motor.axis, dir * rsqrt(distanceSq));
    }
}

void ApplyMotorMoving(float voxelSize, float3 cellPosWS, MotorMoving motor, inout float3 velocityWS)
{
    float3 dirPre = cellPosWS - motor.prePosition ;
    float moveLen = dot(dirPre, motor.moveDir);
    moveLen = min(max(0, moveLen), motor.moveLen);
    float3 curPos = moveLen * motor.moveDir + motor.prePosition ;
    float3 dirCur = cellPosWS - curPos;
    float distanceSq = LengthSq(dirCur) + 0.001f;
    float moveVelocity = length(motor.moveDir);
    if (distanceSq < motor.radiusSq)
    {
        float3 blowDir = rsqrt(distanceSq) * float3(dirCur.x, dirCur.y, dirCur.z) * moveVelocity + motor.moveDir;
        if (length(blowDir) == 0)
        {
            return;
        }
        blowDir = normalize(blowDir);
        velocityWS += blowDir * motor.force * min(1,moveVelocity)  ;
    }
}


#endif