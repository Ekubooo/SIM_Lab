#define RADIX_R 16
#define THREAD_NUM_X 1024

// InputItems is particle number/id/PIndex	
// InputKeys is cell key 

// [1024 ^ 2] or [GroupNum * GroupSize]
RWStructuredBuffer<uint> InputIndex;	
RWStructuredBuffer<uint> InputKeys;
RWStructuredBuffer<uint> SortedIndex;
RWStructuredBuffer<uint> SortedKeys;
RWStructuredBuffer<uint> GlobalPSum;    

// [1024 * 16] or [GroupNum * BucketNum]   
RWStructuredBuffer<uint> DstCounter;

groupshared uint localPrefix[THREAD_NUM_X];

uint numInputs;
uint currIteration;
uint g_BlocksNums;


uint get4Bits(uint num, int i)
{
    // i is current iteration (pass)
    return ((num >> i*4) & 0xf);
}

uint4 get4Bits(uint4 num,int i)
{
    return ((num >> i*4) & 0xf);
}



void PrefixSumLocal(uint IGid : SV_GroupIndex, uint IScanIndex)
{
    uint d = 0;
    uint i = 0;
    uint offset = 1;
    uint totalNum = THREAD_NUM_X;
    
    // Up sweep
    [unroll]
    for (d = totalNum>>1; d > 0; d >>= 1)
    {
        GroupMemoryBarrierWithGroupSync();
        if (IGid < d)
        {
            uint indexA = offset * (2 * IGid + 1) - 1;
            uint indexB = offset * (2 * IGid + 2) - 1;

            localPrefix[indexB] += localPrefix[indexA];
        }
        offset *= 2;
    }

    if (IGid == 0)
    {
        DstCounter[IScanIndex] = localPrefix[totalNum - 1];
        localPrefix[totalNum - 1] = 0;
    }
    
    GroupMemoryBarrierWithGroupSync();

    // Down sweep
    [unroll]
    for (d = 1; d < totalNum; d *= 2)  
    {
        offset >>= 1;
        GroupMemoryBarrierWithGroupSync();

        if (IGid < d)
        {
            uint indexA = offset * (2 * IGid + 1) - 1;
            uint indexB = offset * (2 * IGid + 2) - 1;

            uint sum = localPrefix[indexA];
            localPrefix[indexA] = localPrefix[indexB];
            localPrefix[indexB] += sum;
        }
    }
    
    GroupMemoryBarrierWithGroupSync();
}