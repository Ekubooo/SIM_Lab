## boundary handler
- HLSL version
```
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
```

- taichi version
```
@ti.func
def confine_position_to_boundary(p):
    bmin = particle_radius_in_world
    bmax = ti.Vector([boundary[0], boundary[1], boundary[2]
                      ]) - particle_radius_in_world
    for i in ti.static(range(dim)):
        # Use randomness to prevent particles from sticking into each other after clamping
        if p[i] <= bmin:
            p[i] = bmin + epsilon * ti.random()
        elif bmax[i] <= p[i]:
            p[i] = bmax[i] - epsilon * ti.random()
    return p
```
