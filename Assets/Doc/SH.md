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

- Using/Find Process:

    ```
    GET Key;
    LoopByOffseet();
      StartIndex(offsetKey);
  
  
    ```
