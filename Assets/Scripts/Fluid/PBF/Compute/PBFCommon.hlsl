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
StructuredBuffer<uint> SortedIndices;
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

const float rho0;           // todo: Pass in
const float inv_rho0;       // todo: Pass in
const float lambdaEps;      // todo: Pass in
const float DeltaQ;         // todo: Pass in
const float S_corr_K;       // todo: Pass in, K = 0.1 is good 
const float S_corr_N;       // todo: Pass in, N = 4 is good

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
            density = WPoly6(offsetToNeighbour, smoothingRadius);
        }
    }

    return float2(density, nearDensity);
}
