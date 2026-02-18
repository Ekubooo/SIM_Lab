#include "./SpatialHash3D.hlsl"

// Buffers
RWStructuredBuffer<float3> Positions;
RWStructuredBuffer<float3> PredictedPositions;
RWStructuredBuffer<float3> Velocities;
RWStructuredBuffer<float3> DeltaPos;    // Delta position not init yet.
RWStructuredBuffer<float> Densities;    // not set yet
RWStructuredBuffer<float> LOperator;    // Lagrange Operator not init yet.

RWStructuredBuffer<float3> SortTarget_Positions;
RWStructuredBuffer<float3> SortTarget_PredictedPositions;
RWStructuredBuffer<float3> SortTarget_Velocities;

// Spatial hashing
RWStructuredBuffer<uint> SpatialKeys;
RWStructuredBuffer<uint> SpatialOffsets;
RWStructuredBuffer<uint> SortedIndices;		// RW new
RWStructuredBuffer<float3> Debug;

// Settings     // todo: structurlize 
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

const float rho0;           // check again
const float inv_rho0;       // check again
const float lambdaEps;      // check again
const float DeltaQ;         // check again
const float S_corr_K;       // check again
const float S_corr_N;       // check again

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
// Holds 2 values:
	// [0] = ActiveCount: (num particles alive or spawned in at the start of the frame)
	// [1] = SurvivorCount: (num particles surviving to the next frame -- copied into compact buffer)
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
const float g_Poly6Coff; 					
const float g_SpikyCoff;			
const float g_SpikyGradCoff;	


float LinearKernel(float dst, float radius)
{
	if (dst < radius)
    {
        return 1 - dst / radius;
    }
    return 0;
}

// Poly6
float SmoothingKernelPoly6(float dst, float radius)
{
	if (dst < radius)
	{
		// float scale = 315 / (64 * PI * pow(abs(radius), 9));
		float v = radius * radius - dst * dst;
		return v * v * v * g_Poly6Coff;
	}
	return 0;
}

// Spiky origin
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

// PCG (permuted congruential generator). Thanks to:
// www.pcg-random.org and www.shadertoy.com/view/XlGcRh
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

// Thanks to https://math.stackexchange.com/a/4112622
// Calculates arbitrary normalized vector that is perpendicular to the given direction
float3 CalculateOrthonormal(float3 dir)
{
	float a = sign((sign(dir.x) + 0.5) * (sign(dir.z) + 0.5));
	float b = sign((sign(dir.y) + 0.5) * (sign(dir.z) + 0.5));
	float3 orthoVec = float3(a * dir.z, b * dir.z, -a * dir.x - b * dir.y);
	return normalize(orthoVec);
}

// kernel with radius check.
//W_poly6(r,h) =  315/(64*PI*h^3) * (1-r^2/h^2)^3
float WPoly6(float3 r, float h)
{
	float radius = length(r);
	float result = 0.0f;
	if (radius <= h && radius >= 0)
	{
		// float item = 1 - pow(radius / h, 2);
		float item = h * h - radius * radius; 
		result = g_Poly6Coff * pow(item, 3);	
	}
	return result;
}

//W_Spiky(r,h) = 15/(PI*h^3) * (1-r/h)^6
float WSpiky(float3 r, float h)
{
	float radius = length(r);
	float result = 0.0f;
	if (radius <= h && radius >= 0)
	{
		// float item = 1 - (radius / h);
		float item = h - radius;
		result = g_SpikyCoff * pow(item, 3);
	}
	return result;
}

//W_Spiky_Grad(r,h)= -45/(PI*h^4) * (1-r/h)^2*(r/|r|);
float3 WSpikyGrad(float3 r, float h)
{
	float radius = length(r);
	float3 result = float3(0.0f, 0.0f, 0.0f);
	if (radius < h && radius > 0)
	{
		float item = h - radius;
		result = g_SpikyGradCoff * pow(item, 2) * normalize(r);
	}
	return result;
}


float Remap01(float val, float minVal, float maxVal)
{
    return saturate((val - minVal) / (maxVal - minVal));
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
            
            // density += DensityKernel(dst, smoothingRadius);
            // nearDensity += NearDensityKernel(dst, smoothingRadius);
            density += WPoly6(offsetToNeighbour, smoothingRadius);
            nearDensity = density;
        }
    }

    return float2(density, nearDensity);
}



