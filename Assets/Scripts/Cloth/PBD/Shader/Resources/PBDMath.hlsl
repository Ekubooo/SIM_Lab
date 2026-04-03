RWStructuredBuffer<float4> positions;
RWStructuredBuffer<float3> velocities;
RWStructuredBuffer<float4> normals;
RWStructuredBuffer<float4> prePos;

#define THREAD_X 8
#define THREAD_Y 8

uint4 size;
float3 springKs;
uniform float3 restLengths;
uniform float mass;

#define Cd 0.5
float4 viscousFluidArgs;
#define Uf viscousFluidArgs.xyz
#define Cv viscousFluidArgs.w

float deltaTime;
#define dt deltaTime
#define totalParticleCount size.z
#define L0 restLengths.x
#define M mass

uniform float4 collisionBall;

// [fix] int2 processs negative dir
static int2 SpringDirs[12] = 
{
    {1,0}, {0,1}, {-1,0}, {0,-1},          // structure force
    {-1,-1}, {-1,1}, {1,-1}, {1,1},        // shear force
    {-2,0}, {2,0}, {0,2}, {0,-2}           // flexion(bend) force
};

static uint getIndex(uint2 id)
{
    return id.y * size.x + id.x;
}

static float3 getPosition(uint index){ return positions[index].xyz; }
static float3 getPredicatePosition(uint index){ return prePos[index].xyz; }
static float3 getPosition(uint2 id){ return positions[getIndex(id)].xyz; }
static float3 getVelocity(uint index){ return velocities[index]; }
static float3 getNormal(uint index){ return normals[index].xyz; }

// [fix] pin logic
static bool isPinned(uint2 id)
{
    return id.y == 0 && (id.x == 0 || id.x == size.x - 1);
}

// [fix] boundary detect
static bool isValidateId(int2 id)
{
    return id.x >= 0 && id.x < (int)size.x && id.y >= 0 && id.y < (int)size.y;
}

static float3 calcExForce(uint2 id)
{
    uint index = getIndex(id);
    float3 currVel = getVelocity(index);
    float3 f = float3(0,0,0);
    // damping
    f += -Cd * currVel;
    // gravity
    f += float3(0, -9.8, 0) * M;
    // wind
    float3 normal = getNormal(index);
    f += Cv * (dot(normal, Uf - currVel)) * normal;
    return f;
}

static int2 normalCompuDirs[4] = { {1,0}, {0,1}, {-1,0}, {0,-1} };

static void updateNormal(uint2 id)
{
    float3 p = getPosition(id);
    float3 normal = float3(0,0,0);
    for(uint i = 0; i < 4; i ++)
    {
        uint j = (i + 1) % 4;
        // [fix] transform into int2 for safe coord offset
        int2 id1 = (int2)id + normalCompuDirs[i];
        int2 id2 = (int2)id + normalCompuDirs[j];
        if(isValidateId(id1) && isValidateId(id2))
        {
            float3 p1 = getPosition((uint2)id1);
            float3 p2 = getPosition((uint2)id2);
            float3 e1 = p1 - p;
            float3 e2 = p2 - p;
            normal += normalize(cross(e1,e2));
            break;
        }
    }
    normals[getIndex(id)] = float4(normalize(normal),0);
}

// collision constrain on prePos
static void solveCollision(uint index)
{
    float3 pos = prePos[index].xyz;
    float3 bCenter = collisionBall.xyz;
    float bRadius = collisionBall.w;
    float disToBall = distance(pos, bCenter) - bRadius;
    if(disToBall < 0)
    {
        float3 e = normalize(pos - bCenter);
        prePos[index] = float4(pos - disToBall * e, prePos[index].w);
    }
}

