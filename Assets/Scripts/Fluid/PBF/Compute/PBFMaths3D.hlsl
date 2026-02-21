#include "./SpatialHash3D.hlsl"

static const float PI = 3.1415926;
static const int ThreadGroupSize = 256;
static const float epsilon = 1e-5;
static const float preDeltaTime = 1/120.0;

// Buffers
RWStructuredBuffer<float3> Positions;
RWStructuredBuffer<float3> PredictedPositions;
RWStructuredBuffer<float3> Velocities;
RWStructuredBuffer<float3> DeltaPosition;
RWStructuredBuffer<float> Densities; 
RWStructuredBuffer<float> LOperator;               


// Spatial hashing
RWStructuredBuffer<uint> SpatialKeys;
RWStructuredBuffer<uint> SpatialOffsets;
StructuredBuffer<uint> SortedIndices;

RWStructuredBuffer<float3> Debug;

RWStructuredBuffer<float3> SortTarget_Positions;
RWStructuredBuffer<float3> SortTarget_PredictedPositions;
RWStructuredBuffer<float3> SortTarget_Velocities;

// Settings
const uint numParticles;
const float gravity;
const float deltaTime;
const float simTime;
const float collisionDamping;
const float smoothingRadius;
const float targetDensity;
const float pressureMultiplier;
const float nearPressureMultiplier;
const float viscosityStrength;
const float edgeForce;
const float edgeForceDst;
const float3 boundsSize;

const float4x4 localToWorld;
const float4x4 worldToLocal;

const float2 interactionInputPoint;
const float interactionInputStrength;
const float interactionInputRadius;

const float g_MaxDeltaVel;      

// PBF params       
const float rho0;
const float inv_rho0;
const float lambdaEps;
const float deltaQ;
const float S_corr_K;
const float S_corr_N;

// Volume texture settings
RWTexture3D<float> DensityMap;
const uint3 densityMapSize;

// ---- Foam, spray, and bubbles ----
struct WhiteParticle
{
    float3 position;
    float3 velocity;
    float remainingLifetime;
    float scale;
};

RWStructuredBuffer<WhiteParticle> WhiteParticles;
RWStructuredBuffer<WhiteParticle> WhiteParticlesCompacted;
RWStructuredBuffer<uint> WhiteParticleCounters;
const uint MaxWhiteParticleCount;
const float whiteParticleDeltaTime;

const float3 trappedAirParams;
const float2 kineticEnergyParams;
const float bubbleBuoyancy;
const int bubbleClassifyMinNeighbours;
const int sprayClassifyMaxNeighbours;
const float bubbleScale;
const float bubbleScaleChangeSpeed;


const float K_SpikyPow2;
const float K_SpikyPow3;
const float K_SpikyPow2Grad;
const float K_SpikyPow3Grad;

float LinearKernel(float dst, float radius)
{
	if (dst < radius)
    {
        return 1 - dst / radius;
    }
    return 0;
}

float SmoothingKernelPoly6(float dst, float radius)
{
	if (dst < radius)
	{
		float scale = 315 / (64 * PI * pow(abs(radius), 9));
		float v = radius * radius - dst * dst;
		return v * v * v * scale;
	}
	return 0;
}

float SpikyKernelPow3(float dst, float radius)
{
	if (dst < radius)
	{
		float v = radius - dst;
		return v * v * v * K_SpikyPow3;
	}
	return 0;
}

//Integrate[(h-r)^2 r^2 Sin[θ], {r, 0, h}, {θ, 0, π}, {φ, 0, 2*π}]
float SpikyKernelPow2(float dst, float radius)
{
	if (dst < radius)
	{
		float v = radius - dst;
		return v * v * K_SpikyPow2;
	}
	return 0;
}

float DerivativeSpikyPow3(float dst, float radius)
{
	if (dst <= radius)
	{
		float v = radius - dst;
		return -v * v * K_SpikyPow3Grad;
	}
	return 0;
}

float DerivativeSpikyPow2(float dst, float radius)
{
	if (dst <= radius)
	{
		float v = radius - dst;
		return -v * K_SpikyPow2Grad;
	}
	return 0;
}

float ViscosityKernel(float dst, float radius)
{
	if (dst < radius)
	{
		float scale = 15 / (2 * PI * pow(abs(radius), 3));
		float v = dst/radius;
		float a1 = -1/2.0 * pow(v, 3);
		float a2 = pow(v, 2);
		float a3 = 1/2.0 * v;
		return scale * (a1 + a2 + a3 - 1.0);
	}
	return 0;
}

float DensityKernel(float dst, float radius)
{
	//return SmoothingKernelPoly6(dst, radius);
	return SpikyKernelPow2(dst, radius);
}

float NearDensityKernel(float dst, float radius)
{
	return SpikyKernelPow3(dst, radius);
}

float DensityDerivative(float dst, float radius)
{
	return DerivativeSpikyPow2(dst, radius);
}

float NearDensityDerivative(float dst, float radius)
{
	return DerivativeSpikyPow3(dst, radius);
}


float Remap01(float val, float minVal, float maxVal)
{
    return saturate((val - minVal) / (maxVal - minVal));
}

float2 CalculateDensitiesAtPoint(float3 pos)
{
    int3 originCell = GetCell3D(pos, smoothingRadius);
    float sqrRadius = smoothingRadius * smoothingRadius;
    float density = 0;
    float nearDensity = 0;

    // Neighbour search
    for (int i = 0; i < 27; i++)
    {
        uint hash = HashCell3D(originCell + offsets3D[i]);
        uint key = KeyFromHash(hash, numParticles);
        uint currIndex = SpatialOffsets[key];

        while (currIndex < numParticles)
        {
            uint neighbourIndex = currIndex;
            currIndex++;

            uint neighbourKey = SpatialKeys[neighbourIndex];
            // Exit if no longer looking at correct bin
            if (neighbourKey != key)
                break;

            float3 neighbourPos = PredictedPositions[neighbourIndex];
            float3 offsetToNeighbour = neighbourPos - pos;
            float sqrDstToNeighbour = dot(offsetToNeighbour, offsetToNeighbour);

            // Skip if not within radius
            if (sqrDstToNeighbour > sqrRadius)
                continue;

            // Calculate density and near density
            float dst = sqrt(sqrDstToNeighbour);
            density += DensityKernel(dst, smoothingRadius);
            nearDensity += NearDensityKernel(dst, smoothingRadius);
        }
    }

    return float2(density, nearDensity);
}

uint NextRandom(inout uint state)
{
    state = state * 747796405 + 2891336453;
    uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result;
}

float RandomValue(inout uint state)
{
    return NextRandom(state) / 4294967295.0; // 2^32 - 1
}

float3 CalculateOrthonormal(float3 dir)
{
    float a = sign((sign(dir.x) + 0.5) * (sign(dir.z) + 0.5));
    float b = sign((sign(dir.y) + 0.5) * (sign(dir.z) + 0.5));
    float3 orthoVec = float3(a * dir.z, b * dir.z, -a * dir.x - b * dir.y);
    return normalize(orthoVec);
}

void ResolveCollisions(inout float3 pos, inout float3 vel, float collisionDamping)
{
    // Transform position/velocity to the local space of the bounding box
    float3 posLocal = mul(worldToLocal, float4(pos, 1)).xyz;
    float3 velocityLocal = mul(worldToLocal, float4(vel, 0)).xyz;

    // Calculate distance from box on each axis (negative values are outside box)
    const float3 halfSize = 0.5;
    const float3 edgeDst = halfSize - abs(posLocal);

    // Resolve collisions
    if (edgeDst.x <= 0)
    {
        posLocal.x = halfSize.x * sign(posLocal.x);
        velocityLocal.x *= -1 * collisionDamping;
    }
    if (edgeDst.y <= 0)
    {
        posLocal.y = halfSize.y * sign(posLocal.y);
        velocityLocal.y *= -1 * collisionDamping;
    }
    if (edgeDst.z <= 0)
    {
        posLocal.z = halfSize.z * sign(posLocal.z);
        velocityLocal.z *= -1 * collisionDamping;
    }

    // Transform resolved position/velocity back to world space
    pos = mul(localToWorld, float4(posLocal, 1)).xyz;
    vel = mul(localToWorld, float4(velocityLocal, 0)).xyz;
}

void PosClapming2(inout float3 pos)
{
    // complicated one
    uint seedX = asuint(pos.x);
    uint seedY = asuint(pos.y);
    uint seedZ = asuint(pos.z);
    uint rngState = seedX * seedY * seedZ;
    NextRandom(rngState);
    
    float3 posLocal = mul(worldToLocal, float4(pos, 1)).xyz;
    float localScale = length(mul(worldToLocal, float4(1, 0, 0, 0)).xyz);
    float LPRadius = smoothingRadius * localScale;
    const float3 effectRadius = float3(0.5, 0.5, 0.5) - float3(LPRadius ,LPRadius,LPRadius);
    const float3 edgeDst = effectRadius - abs(posLocal);

    if (edgeDst.x <= 0)
    {
        float jitterX = RandomValue(rngState) * epsilon;
        posLocal.x = (effectRadius.x - jitterX) * sign(posLocal.x);
    }
    if (edgeDst.y <= 0)
    {
        float jitterY = RandomValue(rngState) * epsilon;
        posLocal.y = (effectRadius.y - jitterY) * sign(posLocal.y);
    }
    if (edgeDst.z <= 0)
    {
        float jitterZ = RandomValue(rngState) * epsilon;
        posLocal.z = (effectRadius.z - jitterZ) * sign(posLocal.z);
    }
    
    pos = mul(localToWorld, float4(posLocal, 1)).xyz;
}

void PosClapming1(inout float3 pos)
{
    // Transform position/velocity to the local space of the bounding box
    float3 posLocal = mul(worldToLocal, float4(pos, 1)).xyz;

    // Calculate distance from box on each axis (negative values are outside box)
    const float3 halfSize = 0.5;
    const float3 edgeDst = halfSize - abs(posLocal);

    // Resolve collisions
    if (edgeDst.x <= 0) posLocal.x = halfSize.x * sign(posLocal.x);
    if (edgeDst.y <= 0) posLocal.y = halfSize.y * sign(posLocal.y);
    if (edgeDst.z <= 0) posLocal.z = halfSize.z * sign(posLocal.z);
    
    // Transform resolved position/velocity back to world space
    pos = mul(localToWorld, float4(posLocal, 1)).xyz;
}

void PosClapming(inout float3 pos)
{
    uint seedX = asuint(pos.x);
    uint seedY = asuint(pos.y);
    uint seedZ = asuint(pos.z);
    uint rngState = seedX * seedY * seedZ;
    NextRandom(rngState);
    
    float3 posLocal = mul(worldToLocal, float4(pos, 1)).xyz;
    const float3 halfSize = 0.5;
    const float3 edgeDst = halfSize - abs(posLocal);

    if (edgeDst.x <= 0)
    {
        float jitterX = RandomValue(rngState) * epsilon;
        posLocal.x = (halfSize.x - jitterX) * sign(posLocal.x);
    }
    if (edgeDst.y <= 0)
    {
        float jitterY = RandomValue(rngState) * epsilon;
        posLocal.y = (halfSize.y - jitterY) * sign(posLocal.y);
    }
    if (edgeDst.z <= 0)
    {
        float jitterZ = RandomValue(rngState) * epsilon;
        posLocal.z = (halfSize.z - jitterZ) * sign(posLocal.z);
    }
    
    pos = mul(localToWorld, float4(posLocal, 1)).xyz;
}

