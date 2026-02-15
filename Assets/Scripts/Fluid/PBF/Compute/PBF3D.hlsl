static const float PI = 3.1415926535;

const float K_SpikyPow2;
const float K_SpikyPow3;
const float K_SpikyPow2Grad;
const float K_SpikyPow3Grad;
const float g_Poly6Coff = 315/(PI * 64);	// can pass_in, not set it here
const float g_SpikyCoff = 15/PI;			// can pass_in, not set it here
const float g_SpikyGradCoff = 45/PI;		// can pass_in, not set it here


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

//W_poly6(r,h) =  315/(64*PI*h^3) * (1-r^2/h^2)^3
float WPoly6(float3 r, float h)
{
	float radius = length(r);
	float res = 0.0f;
	if (radius <= h && radius >= 0)
	{
		float item = 1 - pow(radius / h, 2);
		res = g_Poly6Coff / pow(h, 3)  * pow(item, 3);		// ?
	}
	return res;
}

//W_Spiky(r,h) = 15/(PI*h^3) * (1-r/h)^6
float WSpiky(float3 r, float h)
{
	float radius = length(r);
	float res = 0.0f;
	if (radius <= h && radius >= 0)
	{
		float item = 1 - (radius / h);
		res = g_SpikyCoff * pow(item, 6);
	}
	return res;
}

//W_Spiky_Grad(r,h)= -45/(PI*h^4) * (1-r/h)^2*(r/|r|);
float3 WSpikyGrad(float3 r, float h)
{
	float radius = length(r);
	float3 res = float3(0.0f, 0.0f, 0.0f);
	if (radius < h && radius > 0)
	{
		float item = 1 - (radius / h);
		res = g_SpikyGradCoff * pow(item, 2) * normalize(r);
	}
	return res;
}
