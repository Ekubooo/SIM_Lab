using NUnit.Framework;
using UnityEngine;
using System.Diagnostics; // 用于 Stopwatch 计时
using Seb.GPUSorting;
using System;
using System.Linq;

public class GPURadixSortTests
{
    [Test]
    public void RadixSort_LargeRandomData_IsCorrectAndFast()
    {
        // 1. Arrange: 256K particles
        int count = 1024 * 256; 
        
        uint[] keysCpu = new uint[count];
        uint[] indicesCpu = new uint[count];
        System.Random rnd = new System.Random(42); 

        for (int i = 0; i < count; i++)
        {
            keysCpu[i] = (uint)rnd.Next(0, 10000); 
            indicesCpu[i] = (uint)i;               
        }

        // CPU calculate (Oracle)
            // Stopwatch as a timer
        Stopwatch cpuTimer = Stopwatch.StartNew();
        var expectedSorted = keysCpu
            .Select((k, i) => new { Key = k, Index = indicesCpu[i] })
            .OrderBy(x => x.Key)
            .ToArray();
        cpuTimer.Stop();
        
        // UnityEngine.Debug.Log($"[CPU 排序耗时] {cpuTimer.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"[CPU Sorting time] {cpuTimer.ElapsedMilliseconds} ms");

        // Init GPU data 
        ComputeBuffer keysBuffer = new ComputeBuffer(count, sizeof(uint));
        ComputeBuffer indicesBuffer = new ComputeBuffer(count, sizeof(uint));
        keysBuffer.SetData(keysCpu);
        indicesBuffer.SetData(indicesCpu);

        GPURadixSort sorter = new GPURadixSort();

        // 2. Act: run GPU sorting
        // Warmup for compling Shader
        sorter.Run(indicesBuffer, keysBuffer); 
        
        // refill the unorder data 
        keysBuffer.SetData(keysCpu);
        indicesBuffer.SetData(indicesCpu);

        Stopwatch gpuTimer = Stopwatch.StartNew();
        
        sorter.Run(indicesBuffer, keysBuffer); // GPU Sorting
        
        // 3. read back  (Crucially: GetData will block the CPU until the GPU completes)
        uint[] keysGpu = new uint[count];
        uint[] indicesGpu = new uint[count];
        keysBuffer.GetData(keysGpu);
        indicesBuffer.GetData(indicesGpu);
        
        gpuTimer.Stop();
        
        // UnityEngine.Debug.Log($"[GPU 排序及回读耗时] {gpuTimer.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"[GPU Sorting time] {gpuTimer.ElapsedMilliseconds} ms");
        

        // 4. Assert
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(expectedSorted[i].Key, keysGpu[i], $"哈希值排序错误于索引 {i}");
            
            // 注意：如果你的 Radix Sort 是绝对稳定的，可以连 Index 一起 Assert
            // 如果遇到同 Key 下 Index 不一致，说明排序算法是不稳定的，只需注释掉下面这行即可
            // Assert.AreEqual(expectedSorted[i].Index, indicesGpu[i], $"索引匹配错误于 {i}");
        }

        // 5. clear mem
        keysBuffer.Release();
        indicesBuffer.Release();
        sorter.Release();
    }
}