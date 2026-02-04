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
    - Algorithm
    ```
    Start();        // INIT
    Update()
        RunSimulationFrame()
            UpdateSetting();            // For external changing;
            For: i = 0 to PNum 
                apply force to vi      
                predict position xi*
            END For;
            spatialHash.Run();
            For: i = 0 to solverIterations
                For: i = 0 to PNum 
                    calculate LagrangeOperator
                For: i = 0 to PNum              
                    calculate Δpi               
                    collision and detection     
                For: i = 0 to PNum 
                    update pos xi* = xi* + Δpi
            END For;
            For: i = 0 to PNum
                update velocity;
                vorticity and viscosity;
                update position;
            END For;
		    SimulationStepCompleted?.Invoke();  
        END RunSimulationFrame();
        HandleInput();
    END Update();
    ```

    - code
    ```
    Start();        // INIT
    Update()
        RunSimulationFrame()
            UpdateSetting();            // For external changing;
            ApplyAndPredict.Kernel;
            spatialHash.Run();
            For: i = 0 to solverIterations
                LagrangeOperator.Kernel;        
                deltaPos.Kernel;                // Δpi
                CDAR.Kernel;                    // collision detection and response
                updatePredictPos.Kernel;        // xi* = xi* + Δpi
            END For;
            UpdateVelocity.Kernel;      // (?)
            ApplyVV.Kernel;             // vorticity and viscosity;
            UpdatePosition.Kernel;      // (?)
            SimulationStepCompleted?.Invoke();   // (?)
        END RunSimulationFrame();
        HandleInput();
    END Update();
    ```
