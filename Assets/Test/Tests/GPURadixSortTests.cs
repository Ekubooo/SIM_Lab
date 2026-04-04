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
        // 1. Arrange: 准备海量数据 (例如 256K 个粒子)
        // 保证是 1024 的倍数，展示你们的对齐策略
        int count = 1024 * 256; 
        
        uint[] keysCpu = new uint[count];
        uint[] indicesCpu = new uint[count];
        System.Random rnd = new System.Random(42); // 固定种子保证每次测试一样

        for (int i = 0; i < count; i++)
        {
            keysCpu[i] = (uint)rnd.Next(0, 10000); // 模拟空间哈希值
            indicesCpu[i] = (uint)i;               // 模拟粒子原始索引
        }

        // 使用 CPU 计算标准答案 (Oracle)
        // 使用 Stopwatch 顺便测一下 CPU 排序的时间作为对比
        Stopwatch cpuTimer = Stopwatch.StartNew();
        var expectedSorted = keysCpu
            .Select((k, i) => new { Key = k, Index = indicesCpu[i] })
            .OrderBy(x => x.Key)
            .ToArray();
        cpuTimer.Stop();
        
        // UnityEngine.Debug.Log($"[CPU 排序耗时] {cpuTimer.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"[CPU Sorting time] {cpuTimer.ElapsedMilliseconds} ms");

        // 准备 GPU 数据
        ComputeBuffer keysBuffer = new ComputeBuffer(count, sizeof(uint));
        ComputeBuffer indicesBuffer = new ComputeBuffer(count, sizeof(uint));
        keysBuffer.SetData(keysCpu);
        indicesBuffer.SetData(indicesCpu);

        GPURadixSort sorter = new GPURadixSort();

        // 2. Act: 执行 GPU 排序并计时
        // 先跑一次热身 (Warmup)，编译 Shader 会消耗时间，不计入正式成绩
        sorter.Run(indicesBuffer, keysBuffer); 
        
        // 重新填入乱序数据进行正式计时测试
        keysBuffer.SetData(keysCpu);
        indicesBuffer.SetData(indicesCpu);

        Stopwatch gpuTimer = Stopwatch.StartNew();
        
        sorter.Run(indicesBuffer, keysBuffer); // 派发 GPU 任务
        
        // 3. 回读数据 (极其关键：GetData 会阻塞 CPU 直到 GPU 完成，这样计得的时间才真实)
        uint[] keysGpu = new uint[count];
        uint[] indicesGpu = new uint[count];
        keysBuffer.GetData(keysGpu);
        indicesBuffer.GetData(indicesGpu);
        
        gpuTimer.Stop();
        
        // UnityEngine.Debug.Log($"[GPU 排序及回读耗时] {gpuTimer.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"[GPU Sorting time] {gpuTimer.ElapsedMilliseconds} ms");
        

        // 4. Assert: 验证正确性
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(expectedSorted[i].Key, keysGpu[i], $"哈希值排序错误于索引 {i}");
            
            // 注意：如果你的 Radix Sort 是绝对稳定的，可以连 Index 一起 Assert
            // 如果遇到同 Key 下 Index 不一致，说明排序算法是不稳定的，只需注释掉下面这行即可
            // Assert.AreEqual(expectedSorted[i].Index, indicesGpu[i], $"索引匹配错误于 {i}");
        }

        // 5. 清理显存
        keysBuffer.Release();
        indicesBuffer.Release();
        sorter.Release();
    }
}