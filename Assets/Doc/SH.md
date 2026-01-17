## Spatial Hash
- cell size equals to smoothing radius.
- size of hash table is two times of particle number.
- Size for (2 * numParticles) not Verify, TODO.

- INIT Process:

    ```
    Size = numParticles
    INIT Array[Size] Entry -> (0,0);                // for particles.
    INIT Array[Size] StartIndex -> MaxInt;          // for Start Index.
    Position2CellKey();
    Array.sort(Entry);                              // sort by cellKey
    
    ```

- SH lookup process
    - CPU callback version
    ```
    getNeighbor(pos, () =>
    {
        Neighbor.SmoothingKernelFunc();
    });
    ```

    - GPU shader version
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