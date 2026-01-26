using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seb.Helpers;

// todo: dispatch number incorrect. (check)
// todo: is PNum needed? (check compute shader)
// todo: can GroupSize change?
// todo: change para name at last. (when everything done)

namespace Seb.GPUSorting
{
	public class Radix_Debug
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
		static readonly int ID_counterNums = Shader.PropertyToID("g_CounterNums");	
		
		readonly ComputeShader cs = ComputeHelper.LoadComputeShader("RadixSort");	
		
		ComputeBuffer sortedIndexBuffer;
		ComputeBuffer sortedKeyBuffer;
		ComputeBuffer BucketCounterBuffer;
		ComputeBuffer DstCounterBuffer;
		ComputeBuffer GlobalPSumBuffer;
		
		
		public void Run(ComputeBuffer indexBuffer, ComputeBuffer keysBuffer)
		{
			int count = indexBuffer.count;			// ?? to be padding
			// padding by SH.init now				// input should padding before invoke
						
			// where to put? is ok here?
			int InBlockKernel  = cs.FindKernel("InBlockRadix");
			int OvBlockKernel  = cs.FindKernel("OvBlockRadix");
			int GScatterKernel = cs.FindKernel("GlobalScatter");
			
			// int BlockNum = count / getGroupsize(cs);
			// int BlockNum = count / getGroupsize(cs, InBlockKernel);
			int BlockNum = count / 1024;
			int counterNum = 16 * BlockNum;

			// ---- Init ----
			cs.SetInt(ID_numInputs, count);		
			cs.SetInt(ID_blocksNums, BlockNum);		
			cs.SetInt(ID_counterNums, counterNum);		
			
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedIndexBuffer, count))	
			{	// size equals to input
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedKeyBuffer, count))		
			{	// size equals to input
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer); 
			}
				
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref BucketCounterBuffer, counterNum))	
			{	// [1024 * 16] or [GroupNum * BucketNum] 
				cs.SetBuffer(InBlockKernel, ID_BucketCounter, BucketCounterBuffer);
				cs.SetBuffer(OvBlockKernel, ID_BucketCounter, BucketCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref DstCounterBuffer, counterNum))	
			{	// [1024 * 16] or [GroupNum * BucketNum]
				cs.SetBuffer(OvBlockKernel, ID_DstCounter, DstCounterBuffer);
				cs.SetBuffer(GScatterKernel, ID_DstCounter, DstCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref GlobalPSumBuffer, count))	
			{	// size equals to input
				cs.SetBuffer(InBlockKernel, ID_GlobalPSum, GlobalPSumBuffer);
				cs.SetBuffer(GScatterKernel, ID_GlobalPSum, GlobalPSumBuffer);
			}
			
			
			for (int i = 0; i < 8; i++) 
			{
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);	// 1
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer);		// 2
				cs.SetBuffer(GScatterKernel ,ID_InputIndex, indexBuffer);			// 3
				cs.SetBuffer(InBlockKernel ,ID_InputKeys, keysBuffer);				// 4
				cs.SetBuffer(OvBlockKernel ,ID_InputKeys, keysBuffer);				// 4
				cs.SetBuffer(GScatterKernel ,ID_InputKeys, keysBuffer);				// 4
				
				cs.SetInt(ID_CurrIteration, i);	
				
				// Dispatch count/GroupSize = count/1024.
				// need: 1024 ^2	or	[GroupNum * GroupSize]
				ComputeHelper.Dispatch(cs, numIterationsX: count, kernelIndex: InBlockKernel);
				// need: 16	* 1024	or	[GroupNum * BucketNum]  
				ComputeHelper.Dispatch(cs, numIterationsX: counterNum, kernelIndex: OvBlockKernel);
				// need: 1024 ^2	or	[GroupNum * GroupSize]
				ComputeHelper.Dispatch(cs, numIterationsX: count, kernelIndex: GScatterKernel);

				// options : setBuffer switch or kernel CopyBack switch.
				(sortedIndexBuffer, indexBuffer) = (indexBuffer, sortedIndexBuffer);
				(sortedKeyBuffer, keysBuffer) = (keysBuffer, sortedKeyBuffer);
				
			}
				// indexBuffer, keysBuffer now is result (EScan).
		}

		public void Release()
		{
			ComputeHelper.Release(sortedIndexBuffer, sortedKeyBuffer, BucketCounterBuffer, DstCounterBuffer, GlobalPSumBuffer);
		}

		int getGroupsize(ComputeShader cs, int kernelIndex = 0)
		{
			cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y ,out uint z);
			return (int)(x * y * z);	
		}
		
	}
}


// 1024 groups, 256 thread per groups
// --------------- 1024 group ----------------
// ---256 uint---		---256 uint---		 :
// 16			:  ...	16		     :	...	 :
// --------------		--------------		 :
// -------------------------------------------
