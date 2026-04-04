using NUnit.Framework;
using UnityEngine;
using Seb.GPUSorting; // 引用你的命名空间
using Seb.Helpers;

public class ScanTests
{
    [Test]
    public void Scan_WithArrayOfOnes_CalculatesCorrectPrefixSum()
    {
        // 1. Arrange: 准备数据
        int count = 1024 * 4; // 必须是你处理好的 1024 的倍数
        uint[] inputCpu = new uint[count];
        
        // 我们用最简单的测试用例：全是 1 的数组 [1, 1, 1, 1...]
        for (int i = 0; i < count; i++)
        {
            inputCpu[i] = 1;
        }

        ComputeBuffer buffer = new ComputeBuffer(count, sizeof(uint));
        buffer.SetData(inputCpu);

        // 2. Act: 运行你的 Scan
        Scan scanObj = new Scan();
        scanObj.Run(buffer);

        // 3. 回读数据
        uint[] resultGpu = new uint[count];
        buffer.GetData(resultGpu);

        // 4. Assert: 验证真理
        // 如果是 Exclusive Scan，[1,1,1,1] 的结果应该是 [0,1,2,3...]
        // 如果是 Inclusive Scan，结果应该是 [1,2,3,4...]
        // 这里以 Exclusive 为例（如果测试失败，你可以把期望值加 1 改为 Inclusive 逻辑）
        for (int i = 0; i < count; i++)
        {
            uint expectedValue = (uint)i; 
            Assert.AreEqual(expectedValue, resultGpu[i], $"Scan 失败在索引 {i}");
        }

        // 5. 清理内存
        buffer.Release();
        scanObj.Release();
    }
}