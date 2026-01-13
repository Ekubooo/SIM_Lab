## SPH GPU
- TODO: 
    - rendering part.
    - Bitonic sort CS impl.

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

                    RunSpatial()
                        gpuSort.Run()               // pIndex, keyIndex
                        spatialOffsetsCalc.Run()    // startIndex
                    END RunSpatial()

                    density.Kernel;
                    pressure.Kernel;
                    viscosity.Kernel;
                    updatePosition.Kernel;
                END RunSimStep();

                // detail for Phy standard (See Q&A for details).
                // not be used yet (using by 3D Foam yet).
				SimulationStepCompleted?.Invoke();
            END For Loop;
        END RunSimulationFrame();
        HandleInput();
    END Update();
    ```

- spatialHash process
    ```
    RunSpatial()
        UpdateSpatialHash.Kernel;

        spatialHash.Run()
            gpuSort.Run(items[], keys[]) 
                cs.SetBuffer();
                ClearCounts.Kernel;
                Count.Kernel;

                scan.Run();

                ScatterOutputs.Kernel;
                CopyBack.Kernel;
            END gpuSort.Run()

            spatialOffsetsCalc.Run(keys[], Offset[])
                init?.Kernel;
                offsets.Kernel;
            END spatialOffsetsCalc.Run();
        END spatialHash.Run();

        reorder.Kernel;
        copyback.Kernel;
    END RunSpatial();
      
    ```
    - data example 
    ```
    PINDEX:  2  5  7  0  1  6  3  4  8  9  
    C_KEY:  [2, 2, 2, 3, 6, 6, 9, 9, 9, 9]
    START:  [∞, ∞, 0, 3, ∞, ∞, 4, ∞, ∞, 6] 
    ```


- GPU Counting sort process
    ```
    gpuSort.Run() 
        cs.Set();
        ClearCounts.Kernel;
            Counts.INIT();
            InputItems.INIT();
        Count.Kernel;
            InterlockedAdd(Counts[key], 1);
        scan.Run();     // Counts now is offset/position of ordered array
        ScatterOutputs.Kernel;
            InterlockedAdd(Counts[key], 1, retIndex);   // conflicit avoid
        CopyBack.Kernel;
            InputItems.WRITE(SortedItems);
            InputKeys.WRITE(SortedKeys);
    END gpuSort.Run()
    ```


- GPU 4-bit Radix sort process (error)
    ```
    gpuSort.Run() 
        cs.Set();
        ClearCounts.Kernel 
            Counts.INIT();
            InputItems.INIT();
        END ClearCounts.Kernel

        For i = 1 to 8  (32 radix sort for 8 pass)
            RadixCount.Kernel
                //(4-bit as 16 buckets)
                [Unroll(16)] For j = 0 to 15  
                    if (equal_bit) bitBucket[i * PNum + id.x] = 1;
            END RadixCount.Kernel
        END For

        // 16 times scan or 1 huge scan?
        scan.Run();     // Counts now is offset/position of ordered array

        // to be continue here...
        ScatterOutputs.Kernel;
            InterlockedAdd(Counts[key], 1, retIndex);   // conflicit avoid
        CopyBack.Kernel;
            InputItems.WRITE(SortedItems);
            InputKeys.WRITE(SortedKeys);
    END gpuSort.Run()
    ```



- Scan process
    ```
    scan.INIT
        Helper.LoadComputeShader();
    scan.Run()
        cs.Set();
        BlockScan.Kernel;
        if numGroups > 1    // Recursive Scan block layer
            scan.Run(groupSumBuffer);
            cs.Set();
            BlockCombine.Kernel;
        END if
    END Run()
    ```

- SCAN Example process for 262144 particles
    ```
    Scan.Run(Elements)
        int numGroups1 = Elements.count / 2 / 256 ;   
            // 256 thread, 1 sum each, so numGroups1 = 512.
        Dictionary.add(512, BufferGS1);  // Buffer pool, size = 512
        cs.SetBuffer(Elements/BufferGS1/count);
        cs.Dispatch(scanKernel, numGroups1/*512*/, 1, 1);     
            // 512 gorups,  256 thread each
        
        BlockScan.kernel        // in block, 1 of 512 block(array)
            INDEX and FLAG;
            groupshared Temp[] = FLAG ? Elements[] : 0;

            UP_Sweep()
                Offset = 1;     // distance of Destination
                // d loop
                For : Act = GROUP_SIZE to 1 by Act/=2 : (256->128->...->2->1)  
                    GroupBarrier();     //  synchronization

                    // ((0-255) < (256->128 ->...1)): summation per d (tree layer)    
                    if (in_Limmit)      // (k loop in parallel) 
                        INDEX A and B by Offset;   // see google doc
                        Temp[B] = Temp[A] + Temp[B]; 
                    Offset *= 2;
                END For (d loop)
                if (threadLocal == 0) 
                    // Temp[last] is IScan
                    // now the IScan of current block is done
                    BufferGS1[blockNum] = Temp[last]; 
                    Temp[last] SET 0;
                End if  
            END UP_Sweep()

            DOWN_Sweep()
                For : Act = 1 to GROUP_SIZE by Act*=2 : (1->2->...->128->256)
                    GroupBarrier();     // synchronization
                    Offset /= 2;

                    // ((0-255) < (1...->128->256)): swap per d (tree layer)    
                    if (in_Limmit)      // (k loop in parallel) 
                        INDEX A and B by Offset;   // see google doc
                        SWAP Temp[];
                END For (d loop)
            END DOWN_Sweep()

            GroupBarrier();             // synchronization

            // Elements is pIndex array
            WRITEBACK: Elements[pIndex] = FLAG ? Temp[pIndex] : 0;
        END BlockScan.kernel

        // now have: Elements(EScan : inter-block order) and BufferGS1
        
        if numGroups1 > 1               // 512 > 1
            // upper 
            Scan.Run(BufferGS1)
                int numGroups2 = 512 / 2 / 256 = 1;
                Dictionary.add(1, BufferGS2)         // size = 1
                cs.SetBuffer(BufferGS1/BufferGS2/count);
                cs.Dispatch(scanKernel, numGroups2/* 1 */, 1, 1);

                // now have: BufferGS1(EScan: order in GS1) and GS2(uint element sum(value))
                // GS2 maybe for upper layer?

                if numGroups2/* 1 */ > 1 ? {...}    // no upper layer, out.
            END Scan.Run(BufferGS1)

            // now have: 
            // Elements(512 local(512) prefix sum)
            // BufferGS1(block prefix sum), BufferGS1;
            cs.SetBuffer(Elements/BufferGS1/count);
            cs.Dispatch(combineKernel, numGroups1/*512*/ , 1, 1);  
 
            BlockCombine.kernel
                // one thread handle 2 element;
                // so 131072 thread handle 262144 particles
                INDEX_A = SV_DispatchThreadID * 2;      
                INDEX_B = SV_DispatchThreadID * 2 + 1;      
                    
                // For now : Elements[] is LocalOffset, GroupSums is GlobalOffset
                if (INDEX.valid) Elements[INDEX_A] += BufferGS1[SV_GroupID]
                if (INDEX.valid) Elements[INDEX_B] += BufferGS1[SV_GroupID]
            END BlockCombine.kernel
            // Now Elements[262144] is overall offset/position for scatter.
        END if

    ```

- SpatialOffsetCalculator process
    ```
    spatialOffsetsCalc.Run()
        init?.Kernel;
            Boundary Check;
            // INIT **StartIndex(Offset)** as PNum(means invalid)
            Offset[].SET(∞);    
        END init?.Kernel;

        offsets.Kernel;     // 256 thread as befor
            Boundary Check;
            Offset[key[i]] = key[i] != key[i-1] ? i : ∞;
        END offsets.Kernel;
    END spatialOffsetsCalc.Run();
    ```

- SH lookup process
    - callback style
    ```
    getNeighbor(pos, () =>
    {
        Neighbor.SmoothingKernelFunc();
    });
    ```

    - lookup process (Shader version)
    ```
    getNeighbor()
        Foreach : round cells
            GET KEY and START_INDEX;

            // loop all particles in curr cell
            WHILE SIndex++ < PNum       
                if (key_out_of_cell) BREAK;
                if (out_of_radius) CONTINUE;
                Neighbor.SmoothingKernelFunc();  
            END WHILE
        END Foreach
    END getNeighbor()
    ```









