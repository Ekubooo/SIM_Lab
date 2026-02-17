static const float PI = 3.1415926535;

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
		float scale = 315 / (64 * PI * pow(abs(radius), 9));
		float v = radius * radius - dst * dst;
		return v * v * v * scale;
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
		float item = h * h - r * r; 
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
		float item = h - r;
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
		float item = h - r;
		result = g_SpikyGradCoff * pow(item, 2) * normalize(r);
	}
	return result;
}
