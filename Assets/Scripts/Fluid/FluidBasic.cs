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

        // 4. ActiveParticleCount needed?
        // padding num in render? or not?
        public abstract int ActiveParticleCount { get; }
        
        protected void NotifyInitCompleted()
        {
            SimulationInitCompleted?.Invoke(this);
        }
    }
}