## Record
- overall 2D process 
    - Pressure Apply
    
      ```
      start();        // initialize var
      update()
          SimStep(deltaTime)
              UPDATE velocity by GRAVITY;
              INIT predictPos;
              DENSITY PreCalculate by predictPos;
              PRESSURE Force and Acc by predictPos;
              UPDATE velocity by PRESSURE;
              UPDATE position by velocity;
              Collisions Handle;
          END SimStep();
          DRAW Particles;
      END update();
      ```
  
## TODO
- Viscosity
- SH now implement by callback func, try do it with simple array return. 
- new presure func applying.
- (SH) Size for (2 * numParticles) not Verify, TODO.