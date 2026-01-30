using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seb.Helpers;

namespace Seb.GPUSorting
{
	public class GPURadixSort
	{
		static readonly int ID_InputIndex = Shader.PropertyToID("InputIndex");
		static readonly int ID_InputKeys = Shader.PropertyToID("InputKeys");
		static readonly int ID_SortedIndex = Shader.PropertyToID("SortedIndex");
		static readonly int ID_SortedKeys = Shader.PropertyToID("SortedKeys");
		static readonly int ID_BucketCounter = Shader.PropertyToID("BucketCounter"); 
		static readonly int ID_DstCounter = Shader.PropertyToID("DstCounter");
		static readonly int ID_GlobalPSum = Shader.PropertyToID("GlobalPSum");
		
		static readonly int ID_CurrIteration = Shader.PropertyToID("currIteration");	
		static readonly int ID_numInputs = Shader.PropertyToID("numInputs");	
		static readonly int ID_blocksNums = Shader.PropertyToID("g_BlocksNums");	
		
		readonly ComputeShader cs = ComputeHelper.LoadComputeShader("RadixSort");
		readonly Scan scan = new Scan(); 
		
		ComputeBuffer sortedIndexBuffer;
		ComputeBuffer sortedKeyBuffer;
		ComputeBuffer DstCounterBuffer;
		ComputeBuffer GlobalPSumBuffer;
		
		public void Run(ComputeBuffer indexBuffer, ComputeBuffer keysBuffer)
		{
			int count = indexBuffer.count;
			int groupSize = 1024;
			int BlockNum = Mathf.CeilToInt((float)count / groupSize);
			int counterNum = 16 * BlockNum;		// bucketNum * BlockNum
			
			int InBlockKernel  = cs.FindKernel("InBlockRadix");
			int GScatterKernel = cs.FindKernel("GlobalScatter");
			
			cs.SetInt(ID_numInputs, count);		
			cs.SetInt(ID_blocksNums, BlockNum);		
			
			// ---- Create Buffers ----
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedIndexBuffer, count))	
			{
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedKeyBuffer, count))		
			{
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer); 
			}
			
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref DstCounterBuffer, counterNum))	
			{
				cs.SetBuffer(InBlockKernel, ID_DstCounter, DstCounterBuffer);
				cs.SetBuffer(GScatterKernel, ID_DstCounter, DstCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref GlobalPSumBuffer, count))	
			{
				cs.SetBuffer(InBlockKernel, ID_GlobalPSum, GlobalPSumBuffer);
				cs.SetBuffer(GScatterKernel, ID_GlobalPSum, GlobalPSumBuffer);
			}
			
			for (int i = 0; i < 8; i++) 
			{
				cs.SetInt(ID_CurrIteration, i);
				
				cs.SetBuffer(InBlockKernel, ID_InputKeys, keysBuffer);
				cs.SetBuffer(GScatterKernel, ID_InputIndex, indexBuffer);
				cs.SetBuffer(GScatterKernel, ID_InputKeys, keysBuffer);
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer);
				
				ComputeHelper.Dispatch(cs, numIterationsX: count, kernelIndex: InBlockKernel);
				scan.Run(DstCounterBuffer);
				ComputeHelper.Dispatch(cs, numIterationsX: count, kernelIndex: GScatterKernel);
				
				(sortedIndexBuffer, indexBuffer) = (indexBuffer, sortedIndexBuffer);
				(sortedKeyBuffer, keysBuffer) = (keysBuffer, sortedKeyBuffer);
			}
			// indexBuffer, keysBuffer now is result (EScan).
		}

		public void Release()
		{
			ComputeHelper.Release(sortedIndexBuffer, sortedKeyBuffer, DstCounterBuffer, GlobalPSumBuffer);
			scan.Release(); 
			
		}
	}
}