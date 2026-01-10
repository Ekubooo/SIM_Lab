## SPH 2D
- TODO: 
    - rendering part.
    - Bitonic sort CS impl reading.
    - recap of 5440 Radix and counting?
    - GPU Radix.

- Overall process of SPH
    ```
    Start()/Init()
        ComputeHelper.SetBuffer();
    END Start()/Init();

    Update()
        RunSimulationFrame()
            UpdateSetting();        // For external changing;
            For: 0 -> IterationPerFrame
  
                RunSimStep()
                    externalForces.Kernel;
                    RunSpatial();   // Process
                    density.Kernel;
                    pressure.Kernel;
                    viscosity.Kernel;
                    updatePosition.Kernel;
                END RunSimStep();

                // detail for Phy standard (See Q&A for details).
                // not be used yet (using by 3D Foam yet).
				SimulationStepCompleted?.Invoke();
            END ForLoop;
        END RunSimulationFrame();
        HandleInput();
    END Update();
    ```

- spatialHash process
    ```
    RunSpatial()
        UpdateSpatialHash.Kernel;

        spatialHash.Run()
            gpuSort.Run() 
                cs.SetBuffer();
                ClearCounts.Kernel;
                Count.Kernel;

                scan.Run();

                ScatterOutputs.Kernel;
                CopyBack.Kernel;
            END gpuSort.Run()

            spatialOffsetsCalc.Run()
                init?.Kernel;
                offsets.Kernel;
            END spatialOffsetsCalc.Run();
        END spatialHash.Run();

        reorder.Kernel;
        copyback.Kernel;
    END RunSpatial();
      
    ```

- GPU sort process
    ```
    gpuSort.Run() 
        cs.Set();
        ClearCounts.Kernel;
            Counts.INIT();
            InputItems.INIT();
        Count.Kernel;
            InterlockedAdd(Counts[key], 1);
        scan.Run();
        ScatterOutputs.Kernel;
            InterlockedAdd(Counts[key], 1, retIndex);   // conflicit avoid
        CopyBack.Kernel;
            InputItems.WRITE(SortedItems);
            InputKeys.WRITE(SortedKeys);
    END gpuSort.Run()
    ```

- Scan process
    ```
    scan()
        Helper.LoadComputeShader();
    scan.Run()
        cs.Set();
        BlockScan.Kernel;
        if numGroups > 1    // Recursive Scan of different block
            scan.Run(groupSumBuffer);
            cs.Set();
            BlockCombine.Kernel;
        END if
    END Run()
    ```