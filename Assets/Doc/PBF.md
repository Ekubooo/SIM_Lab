# PBF impl
- Overall process of SPH Contrast

    ```
    Start();        // INIT
    Update()
        RunSimulationFrame()
            UpdateSetting();        // For external changing;
            For: 0 -> IterationPerFrame
                RunSimStep()
                    externalForces.Kernel;
                    RunSpatial();
                    density.Kernel;
                    pressure.Kernel;
                    viscosity.Kernel;
                    updatePosition.Kernel;
                END RunSimStep();
				SimulationStepCompleted?.Invoke();
            END For Loop;
        END RunSimulationFrame();
        HandleInput();
    END Update();
    ```

- PBF

    ```
    Start();        // INIT
    Update()
        For i = 0 to subIter        // 1 to 3 or more
            UpdateSetting();            
            ApplyAndPredict.Kernel;
            spatialHash.Run();
            // Ligo set collision process here (?)
            While: (i++ < solverIterations) or (err > limit)
                LagrangeOperator.Kernel;        
                DeltaPos.Kernel;                // Δpi
                Collision.Kernel;               // collision detection and response
                UpdatePredictPos.Kernel;        // xi* = xi* + Δpi
            END For;
            UpdateVelocity.Kernel;      
            ApplyVV.Kernel;         // vorticity and viscosity;
            UpdatePosition.Kernel;      
            SimulationStepCompleted?.Invoke();   
        END For;
        HandleInput();
    END Update();
    ```
