using UnityEngine;
using System;
using Seb.Fluid.Simulation;

namespace Seb.Helpers
{
    internal interface InputData
    {
        bool isPaused { get; set; }
        bool inSlowMode { get; set; }
        bool pauseNextFrame { get; set; }
        float gravity { get; set; }
        float RotateSpeed { get; set; }
        Transform transform { get; }
    }
    
    [System.Serializable] 
    public class InputHelper
    {
        public float tempSpeed = 0f;
        
        internal void OnUpdate(InputData owner)
        {
            HandleRotation(ref owner);
            
            if (Input.GetKeyDown(KeyCode.Space))
                owner.isPaused = !owner.isPaused;
            
            if (Input.GetKeyDown(KeyCode.S))
                owner.inSlowMode = !owner.inSlowMode;
            
            if (Input.GetKeyDown(KeyCode.G))
                owner.gravity = -1f * owner.gravity;

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                owner.isPaused = false;
                owner.pauseNextFrame = true;
            }
        }

        void HandleRotation(ref InputData owner)
        {
            Vector3 tempR = owner.transform.localRotation.eulerAngles;
            tempSpeed = 0f;
            float deltaTime = Time.deltaTime;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
            {
                owner.isPaused = false;
                if (Input.GetKey(KeyCode.Q))
                    tempSpeed = 30f;		// SPEED may by FPS and Editor
                else if (Input.GetKey(KeyCode.E))
                    tempSpeed = -30f;
            }

            owner.RotateSpeed = Mathf.Lerp(owner.RotateSpeed, tempSpeed, deltaTime * 10f);
            tempR.y += owner.RotateSpeed * deltaTime;
            owner.transform.localRotation = Quaternion.Euler(tempR);
            
        }
    }
}