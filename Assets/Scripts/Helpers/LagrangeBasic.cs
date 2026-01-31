using UnityEngine;
using System;

namespace Seb.Fluid.Simulation
{
    public abstract class LagrangeBasic : MonoBehaviour 
    {
        public event Action SimulationInitCompleted;
        
        public abstract ComputeBuffer PositionBuffer { get; }
        public abstract int NumParticles { get; }


        public virtual ComputeBuffer VelocityBuffer => null;
        public virtual ComputeBuffer DensityBuffer => null;
        
        // Form Relative can be here as interface
        public virtual ComputeBuffer FoamBuffer => null;
        public virtual ComputeBuffer FoamCountBuffer => null;
        public virtual int MaxFoamParticles => 0;
        
        protected void NotifyInitComplete()
        {
            SimulationInitCompleted?.Invoke();
        }
    }
}