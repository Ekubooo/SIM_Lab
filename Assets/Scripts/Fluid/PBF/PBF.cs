using System;
using UnityEngine;
using Seb.GPUSorting;
using Seb.Helpers;
using Seb.Fluid.Simulation;
using Unity.Mathematics;
using System.Collections.Generic;
using static Seb.Helpers.ComputeHelper;

namespace Seb.Fluid.Simulation
{
	public class PBF : FluidBase, InputData
	{
		public event Action<PBF> SimulationInitCompleted;

		[Header("Time Step")] 
		public float normalTimeScale = 1;
		public float slowTimeScale = 0.1f;
		public float maxTimestepFPS = 60; // if time-step dips lower than this fps, simulation will run slower (set to 0 to disable)
		public int iterationsPerFrame = 3;
		public int maxSolverIterations = 3;

		[Header("Simulation Settings")] 
		public float gravity = -10;
		public float smoothingRadius = 0.2f;
		public float targetDensity = 630;
		public float pressureMultiplier = 288;
		public float nearPressureMultiplier = 2.15f;
		public float viscosityStrength = 0.99f;
		const float MaxDeltaVel = 3.5f;
		[Range(0, 1)] public float collisionDamping = 0.95f;

		[Header("Foam Settings")] 
		public bool foamActive;
		public int maxFoamParticleCount = 1000;
		public float trappedAirSpawnRate = 70;
		public float spawnRateFadeInTime = 0.5f;
		public float spawnRateFadeStartTime = 0;
		public Vector2 trappedAirVelocityMinMax = new(5, 25);
		public Vector2 foamKineticEnergyMinMax = new(15, 80);
		public float bubbleBuoyancy = 1.5f;
		public int sprayClassifyMaxNeighbours = 5;
		public int bubbleClassifyMinNeighbours = 15;
		public float bubbleScale = 0.5f;
		public float bubbleChangeScaleSpeed = 7;

		[Header("Volumetric Render Settings")] 
		public bool renderToTex3D;
		public int densityTextureRes;

		[Header("PBF Params")] 
		public float lambdaEps = 1000f;
		public float S_corr_K = 0f;
		public float S_corr_N = 4f;
		float rho0, deltaQ;

		[Header("References")] 
		public ComputeShader compute;
		public Spawner3D spawner;

		[HideInInspector] public RenderTexture DensityMap;
		public Vector3 Scale => transform.localScale;

		// Buffers
		public ComputeBuffer foamBuffer { get; private set; }
		public ComputeBuffer foamSortTargetBuffer { get; private set; }
		public ComputeBuffer foamCountBuffer { get; private set; }
		
		public ComputeBuffer positionBuffer { get; private set; }
		public ComputeBuffer predictPositionBuffer { get; private set; }
		public ComputeBuffer velocityBuffer { get; private set; }
		public ComputeBuffer deltaPositionBuffer { get; private set; }
		public ComputeBuffer densityBuffer { get; private set; }
		public ComputeBuffer lOperatorBuffer { get; private set; }
		public ComputeBuffer debugBuffer { get; private set; }
		
		// Rendering setting.
		public override ComputeBuffer PositionBuffer => positionBuffer;
		public override ComputeBuffer VelocityBuffer => velocityBuffer;
		public override ComputeBuffer DebugBuffer => debugBuffer;

		// Foam
		public override bool FoamActive => foamActive;
		public override ComputeBuffer FoamBuffer => foamBuffer;
		public override ComputeBuffer FoamCountBuffer => foamCountBuffer;
		public override int MaxFoamParticleCount => maxFoamParticleCount;
		public override int BubbleClassifyMinNeighbours => bubbleClassifyMinNeighbours;
		public override int SprayClassifyMaxNeighbours => sprayClassifyMaxNeighbours;
		public override int ActiveParticleCount => positionBuffer != null ? positionBuffer.count : 0;
		
		
		ComputeBuffer sortTarget_positionBuffer;
		ComputeBuffer sortTarget_velocityBuffer;
		ComputeBuffer sortTarget_predictedPositionsBuffer;

		private int applyAndPredictKernel;
		private int updateSpatialHashKernel;
		private int reorderKernel;
		private int reorderCopyBackKernel;
		private int calcLagrangeOperatorKernel;
		private int calcDeltaPositionKernel;
		private int updatePredictPositionKernel;
		private int updatePropertyKernel;
		private int calcViscosityKernel;	

		SpatialHash spatialHash;

		// State
		internal bool isPaused;
		internal bool pauseNextFrame;
		float smoothRadiusOld;
		float simTimer;
		internal bool inSlowMode;
		Spawner3D.SpawnData spawnData;	
		Dictionary<ComputeBuffer, string> bufferNameLookup;
		
		internal float RotateSpeed = 0f;
		InputHelper inputHelper;
		
		float InputData.gravity { get => gravity; set => gravity = value; }
		float InputData.RotateSpeed { get => RotateSpeed; set => RotateSpeed = value; }
		bool InputData.isPaused { get => isPaused; set => isPaused = value; }
		bool InputData.inSlowMode { get => inSlowMode; set => inSlowMode = value; }
		bool InputData.pauseNextFrame { get => pauseNextFrame; set => pauseNextFrame = value; }
		Transform InputData.transform { get => this.transform; }

		void Start()
		{
			Debug.Log("Controls: Space = Play/Pause, S = SlowMode, R = Reset");
			Debug.Log("Controls: Q/E = Rotation, G = Gravity");
			
			isPaused = false;
			Initialize();
		}

		void Initialize()
		{
			spawnData = spawner.GetSpawnData();
			int numParticles = spawnData.points.Length;

			spatialHash = new SpatialHash(numParticles);
			inputHelper = new InputHelper();
			
			// Kernel ID
			applyAndPredictKernel		= compute.FindKernel("ApplyAndPredict");
			updateSpatialHashKernel		= compute.FindKernel("UpdateSpatialHash");
			reorderKernel				= compute.FindKernel("Reorder");
			reorderCopyBackKernel		= compute.FindKernel("ReorderCopyBack");
			calcLagrangeOperatorKernel	= compute.FindKernel("CalcLagrangeOperator");
			calcDeltaPositionKernel		= compute.FindKernel("CalcDeltaPosition");
			updatePredictPositionKernel	= compute.FindKernel("UpdatePredictPosition");
			updatePropertyKernel		= compute.FindKernel("UpdateProperty");
			calcViscosityKernel			= compute.FindKernel("CalculateViscosity");
			
			// Create buffers
			positionBuffer				= CreateStructuredBuffer<float3>(numParticles);
			predictPositionBuffer		= CreateStructuredBuffer<float3>(numParticles);
			velocityBuffer				= CreateStructuredBuffer<float3>(numParticles);
			deltaPositionBuffer			= CreateStructuredBuffer<float3>(numParticles);
			densityBuffer				= CreateStructuredBuffer<float>(numParticles);
			lOperatorBuffer				= CreateStructuredBuffer<float>(numParticles);
			
			foamBuffer					= CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamSortTargetBuffer		= CreateStructuredBuffer<FoamParticle>(maxFoamParticleCount);
			foamCountBuffer				= CreateStructuredBuffer<uint>(4096);
			debugBuffer					= CreateStructuredBuffer<float3>(numParticles);

			sortTarget_positionBuffer			= CreateStructuredBuffer<float3>(numParticles);
			sortTarget_predictedPositionsBuffer = CreateStructuredBuffer<float3>(numParticles);
			sortTarget_velocityBuffer			= CreateStructuredBuffer<float3>(numParticles);

			bufferNameLookup = new Dictionary<ComputeBuffer, string>
			{
				{ positionBuffer, "Positions" },
				{ predictPositionBuffer, "PredictedPositions" },
				{ velocityBuffer, "Velocities" },
				{ deltaPositionBuffer, "DeltaPosition" },
				{ densityBuffer, "Densities" },
				{ lOperatorBuffer, "LOperator" },
				{ spatialHash.SpatialKeys, "SpatialKeys" },
				{ spatialHash.SpatialOffsets, "SpatialOffsets" },
				{ spatialHash.SpatialIndices, "SortedIndices" },
				{ sortTarget_positionBuffer, "SortTarget_Positions" },
				{ sortTarget_predictedPositionsBuffer, "SortTarget_PredictedPositions" },
				{ sortTarget_velocityBuffer, "SortTarget_Velocities" },
				{ foamCountBuffer, "WhiteParticleCounters" },
				{ foamBuffer, "WhiteParticles" },
				{ foamSortTargetBuffer, "WhiteParticlesCompacted" },
				{ debugBuffer, "Debug" }
			};

			// Set buffer data
			SetInitialBufferData(spawnData);

			// apply and predict kernel
			SetBuffers(compute, applyAndPredictKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictPositionBuffer,
				velocityBuffer
			});
			
			// Spatial hash kernel
			SetBuffers(compute, updateSpatialHashKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictPositionBuffer,
				spatialHash.SpatialKeys
			});

			// Reorder kernel
			SetBuffers(compute, reorderKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictPositionBuffer,
				velocityBuffer,
				sortTarget_positionBuffer,
				sortTarget_predictedPositionsBuffer,
				sortTarget_velocityBuffer,
				spatialHash.SpatialIndices
			});

			// Reorder copyback kernel
			SetBuffers(compute, reorderCopyBackKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictPositionBuffer,
				velocityBuffer,
				sortTarget_positionBuffer,
				sortTarget_predictedPositionsBuffer,
				sortTarget_velocityBuffer
			});
			
			// Lagrange Operator kernel
			SetBuffers(compute, calcLagrangeOperatorKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictPositionBuffer,
				densityBuffer,
				lOperatorBuffer,
				spatialHash.SpatialOffsets,
				spatialHash.SpatialKeys
			});
			
			// Delta Position kernel
			SetBuffers(compute, calcDeltaPositionKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictPositionBuffer,
				lOperatorBuffer,
				deltaPositionBuffer,
				spatialHash.SpatialKeys,
				spatialHash.SpatialOffsets
			});
			
			// Update Predict Position kernel
			SetBuffers(compute, updatePredictPositionKernel, bufferNameLookup, new ComputeBuffer[]
			{
				predictPositionBuffer,
				deltaPositionBuffer
			});
			
			// Update Property kerenl
			SetBuffers(compute, updatePropertyKernel, bufferNameLookup, new ComputeBuffer[]
			{
				positionBuffer,
				predictPositionBuffer,
				velocityBuffer
			});
			
			SetBuffers(compute, calcViscosityKernel, bufferNameLookup, new ComputeBuffer[]
			{
				velocityBuffer,
				positionBuffer,
				predictPositionBuffer,
				spatialHash.SpatialOffsets,
				spatialHash.SpatialKeys
			});

			compute.SetInt("numParticles", positionBuffer.count);
			compute.SetInt("MaxWhiteParticleCount", maxFoamParticleCount);

			UpdateSmoothingConstants();

			// Run single frame of sim with deltaTime = 0 to initialize density texture
			// (so that display can work even if paused at start)
			if (renderToTex3D)
			{
				RunSimulationFrame(0);
			}

			// SimulationInitCompleted?.Invoke(this);
			NotifyInitCompleted();
		}

		void Update()
		{
			// Run simulation
			if (!isPaused)
			{
				float maxDeltaTime = maxTimestepFPS > 0 ? 1 / maxTimestepFPS : float.PositiveInfinity; 
				// If framerate dips too low, run the simulation slower than real-time
				float dt = Mathf.Min(Time.deltaTime * ActiveTimeScale, maxDeltaTime);
				RunSimulationFrame(dt);
			}

			if (pauseNextFrame)
			{
				isPaused = true;
				pauseNextFrame = false;
			}

			HandleInput();
		}

		void RunSimulationFrame(float frameDeltaTime)
		{
			float subStepDeltaTime = frameDeltaTime / iterationsPerFrame;
			UpdateSettings(subStepDeltaTime, frameDeltaTime);

			// Simulation sub-steps
			for (int i = 0; i < iterationsPerFrame; i++)
			{
				simTimer += subStepDeltaTime;
				RunSimulationStep();
			}
			
		}
		

		void RunSimulationStep()
		{
			Dispatch(compute, positionBuffer.count, kernelIndex: applyAndPredictKernel);
			Dispatch(compute, positionBuffer.count, kernelIndex: updateSpatialHashKernel);
			spatialHash.Run(); 
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderKernel);	
			Dispatch(compute, positionBuffer.count, kernelIndex: reorderCopyBackKernel);	
			
			for (int k = 0; k < maxSolverIterations ; k++) 
			{ 
				Dispatch(compute, positionBuffer.count, kernelIndex: calcLagrangeOperatorKernel); 
				Dispatch(compute, positionBuffer.count, kernelIndex: calcDeltaPositionKernel);	 
				Dispatch(compute, positionBuffer.count, kernelIndex: updatePredictPositionKernel); 
			}

			Dispatch(compute, positionBuffer.count, kernelIndex: updatePropertyKernel);			
			Dispatch(compute, positionBuffer.count, kernelIndex: calcViscosityKernel);			
		}

		void UpdateSmoothingConstants()
		{
			float r = smoothingRadius;
			float spikyPow2		= 15 / (2 * Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3		= 15 / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyPow2Grad = 15 / (Mathf.PI * Mathf.Pow(r, 5));
			float spikyPow3Grad = 45 / (Mathf.PI * Mathf.Pow(r, 6));
			
			// float poly6Coff = 315f / (64f * Mathf.PI * Mathf.Pow(r, 3));	// Simplified
			float poly6Coff		= 315f / (64f * Mathf.PI * Mathf.Pow(r, 9));	
			float spikyCoff		= 15f / (Mathf.PI * Mathf.Pow(r, 6));
			float spikyGradCoff = 45f / (Mathf.PI * Mathf.Pow(r, 6));
			// float spikyGradCoff = 45f / (Mathf.PI * Mathf.Pow(r, 6));

			compute.SetFloat("K_SpikyPow2", spikyPow2);
			compute.SetFloat("K_SpikyPow3", spikyPow3);
			compute.SetFloat("K_SpikyPow2Grad", spikyPow2Grad);
			compute.SetFloat("K_SpikyPow3Grad", spikyPow3Grad);
			
			compute.SetFloat("g_Poly6Coff", poly6Coff);
			compute.SetFloat("g_SpikyCoff", spikyCoff);
			compute.SetFloat("g_SpikyGradCoff", spikyGradCoff);

			rho0 = targetDensity;
			deltaQ = 0.3f * smoothingRadius;
			compute.SetFloat("rho0",rho0);
			compute.SetFloat("inv_rho0",1f/rho0);
			compute.SetFloat("deltaQ",deltaQ);	

		}

		void UpdateSettings(float stepDeltaTime, float frameDeltaTime)
		{
			if (smoothingRadius != smoothRadiusOld)
			{
				smoothRadiusOld = smoothingRadius;
				UpdateSmoothingConstants();
			}

			Vector3 simBoundsSize = transform.localScale;
			Vector3 simBoundsCentre = transform.position;

			compute.SetFloat("deltaTime", stepDeltaTime);
			compute.SetFloat("whiteParticleDeltaTime", frameDeltaTime);
			compute.SetFloat("simTime", simTimer);
			compute.SetFloat("gravity", gravity);
			compute.SetFloat("collisionDamping", collisionDamping);
			compute.SetFloat("smoothingRadius", smoothingRadius);
			compute.SetFloat("targetDensity", targetDensity);
			compute.SetFloat("pressureMultiplier", pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
			compute.SetFloat("viscosityStrength", viscosityStrength);
			compute.SetVector("boundsSize", simBoundsSize);
			compute.SetVector("centre", simBoundsCentre);

			compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
			compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

			
			// PBF params
			compute.SetFloat("lambdaEps", lambdaEps);
			compute.SetFloat("S_corr_K",S_corr_K);
			compute.SetFloat("S_corr_N",S_corr_N);
			
			compute.SetFloat("g_MaxDeltaVel", MaxDeltaVel);
			
			// Foam settings
			float fadeInT = (spawnRateFadeInTime <= 0) ? 1 : Mathf.Clamp01((simTimer - spawnRateFadeStartTime) / spawnRateFadeInTime);
			compute.SetVector("trappedAirParams", new Vector3(trappedAirSpawnRate * fadeInT * fadeInT, trappedAirVelocityMinMax.x, trappedAirVelocityMinMax.y));
			compute.SetVector("kineticEnergyParams", foamKineticEnergyMinMax);
			compute.SetFloat("bubbleBuoyancy", bubbleBuoyancy);
			compute.SetInt("sprayClassifyMaxNeighbours", sprayClassifyMaxNeighbours);
			compute.SetInt("bubbleClassifyMinNeighbours", bubbleClassifyMinNeighbours);
			compute.SetFloat("bubbleScaleChangeSpeed", bubbleChangeScaleSpeed);
			compute.SetFloat("bubbleScale", bubbleScale);
			
		}

		void SetInitialBufferData(Spawner3D.SpawnData spawnData)
		{
			positionBuffer.SetData(spawnData.points);
			predictPositionBuffer.SetData(spawnData.points);
			velocityBuffer.SetData(spawnData.velocities);

			foamBuffer.SetData(new FoamParticle[foamBuffer.count]);

			debugBuffer.SetData(new float3[debugBuffer.count]);
			foamCountBuffer.SetData(new uint[foamCountBuffer.count]);
			simTimer = 0;
		}

		void HandleInput()
		{
			inputHelper.OnUpdate(this);
			
			if (Input.GetKeyDown(KeyCode.R))
			{
				pauseNextFrame = true;
				SetInitialBufferData(spawnData);
				if (renderToTex3D) RunSimulationFrame(0);
				// Run single frame of sim with deltaTime = 0 to initialize density texture
				// (so that display can work even if paused at start)
				
			}
		
		}

		private float ActiveTimeScale => inSlowMode ? slowTimeScale : normalTimeScale;

		void OnDestroy()
		{
			foreach (var kvp in bufferNameLookup)
			{
				Release(kvp.Key);
			}

			spatialHash.Release();
		}


		public struct FoamParticle
		{
			public float3 position;
			public float3 velocity;
			public float lifetime;
			public float scale;
		}

		void OnDrawGizmos()
		{
			// Draw Bounds
			var m = Gizmos.matrix;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = new Color(0, 1, 0, 0.5f);
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.matrix = m;
		}
	}
}