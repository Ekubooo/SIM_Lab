using UnityEngine;
using System;

namespace Seb.Helpers
{
    public abstract class FluidBase : MonoBehaviour
    {
        public event Action<FluidBase> SimulationInitCompleted;

        // 2. core buffer
        public abstract ComputeBuffer PositionBuffer { get; }
        public abstract ComputeBuffer VelocityBuffer { get; }
        public abstract ComputeBuffer DebugBuffer { get; }
        
        // 3. Foam buffer relative 
        public abstract bool FoamActive { get; }
        public abstract ComputeBuffer FoamBuffer { get; }
        public abstract ComputeBuffer FoamCountBuffer { get; }
        public abstract int MaxFoamParticleCount { get; }
        
        // foam shader para
        public virtual int BubbleClassifyMinNeighbours => 0;    // default, can override
        public virtual int SprayClassifyMaxNeighbours => 0;

        // 4. 【关键】暴露有效粒子数量
        // 因为你的新算法 padding 了 buffer 大小（比如 1024 对齐），
        // buffer.count 可能大于实际粒子数。渲染时必须用这个值，否则会画出 (0,0,0) 的废点。
        public abstract int ActiveParticleCount { get; }
        
        protected void NotifyInitCompleted()
        {
            SimulationInitCompleted?.Invoke(this);
        }
    }
}