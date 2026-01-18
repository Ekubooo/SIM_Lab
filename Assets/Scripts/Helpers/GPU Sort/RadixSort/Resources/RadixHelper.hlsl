#define RADIX_R 16
#define THREAD_NUM_X 1024
#define GROUP_SIZE 1024

uint get4Bits(uint num, int i)
{
    // i is current iteration (pass)
    return ((num >> i*4) & 0xf);
}

uint4 get4Bits(uint4 num,int i)
{
    return ((num >> i*4) & 0xf);
}