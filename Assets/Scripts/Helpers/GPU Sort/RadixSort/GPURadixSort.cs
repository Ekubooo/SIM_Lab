using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seb.Helpers;

// todo: sort the input.
// todo: dispatch number incorrect. (upper layer)
// todo: padding in SPH.cs, SH.init()	?!
// todo: the input should padding before invoke gpusort.run
// todo: is PNum needed?
// todo: can GroupSize change?
// todo: hash calculate incorrect. (now model by PNum)
// do cell key and PIndex need init in pass loop? 
	// no because data in pass loop is not the result; and will resulted after 8-pass.
	// every sim_step reCalculating the cellkey-hash, and **PIndex**.

// how to padding?
	// padding at hash kernel: input size = sorting buffer.

// where to init index? already has cellkeys.
	// in hash kernel now.

namespace Seb.GPUSorting
{
	public class GPURadixSort
	{
		// to be change
		static readonly int ID_InputIndex = Shader.PropertyToID("InputIndex");
		static readonly int ID_InputKeys = Shader.PropertyToID("InputKeys");
		
		static readonly int ID_SortedIndex = Shader.PropertyToID("SortedIndex");
		static readonly int ID_SortedKeys = Shader.PropertyToID("SortedKeys");
		static readonly int ID_BucketCounter = Shader.PropertyToID("BucketCounter");
		static readonly int ID_DstCounter = Shader.PropertyToID("DstCounter");
		static readonly int ID_GlobalPSum = Shader.PropertyToID("GlobalPSum");
		
		static readonly int ID_CurrIteration = Shader.PropertyToID("currIteration");	
		static readonly int ID_numInputs = Shader.PropertyToID("numInputs");	
		
		readonly ComputeShader cs = ComputeHelper.LoadComputeShader("RadixSort");	
		
		ComputeBuffer sortedIndexBuffer;
		ComputeBuffer sortedKeyBuffer;
		ComputeBuffer BucketCounterBuffer;
		ComputeBuffer DstCounterBuffer;
		ComputeBuffer GlobalPSumBuffer;
		
		// new
		private int InBlockKernel;
		private int OvBlockKernel;
		private int GScatterKernel;

		private uint BlockNum;
		private uint TotalNum;	// should init at SPH.cs
		private uint counterNum;
		
		public void Run(ComputeBuffer indexBuffer, ComputeBuffer keysBuffer)
		{
			int count = indexBuffer.count;			// ?? to be padding
													// input should padding before invoke
													
			InBlockKernel  = cs.FindKernel("InBlockRadix");
			OvBlockKernel  = cs.FindKernel("OvBlockRadix");
			GScatterKernel = cs.FindKernel("GlobalScatter");
			
			calcSize(count);
			BlockNum =(uint)(count / 1024);

			cs.SetInt(ID_numInputs, count);		
			
			// ---- Init ----
			// count not right

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedIndexBuffer, count))	
			{	// size equals to input
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedKeyBuffer, count))		
			{	// size equals to input
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer); 
			}
				
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref BucketCounterBuffer, count))	
			{	// [1024 * 16] or [GroupNum * BucketNum] 
				cs.SetBuffer(InBlockKernel, ID_BucketCounter, BucketCounterBuffer);
				cs.SetBuffer(OvBlockKernel, ID_BucketCounter, BucketCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref DstCounterBuffer, count))	
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
				
				cs.SetInt(ID_numInputs, i);		// current iteration.
				// Dispatch count/GroupSize = count/1024.
				// need: 1024 ^2
				ComputeHelper.Dispatch(cs, count, kernelIndex: InBlockKernel);
				// need: 16	* 1024	
				ComputeHelper.Dispatch(cs, count, kernelIndex: OvBlockKernel);
				// need: 1024 ^2
				ComputeHelper.Dispatch(cs, count, kernelIndex: GScatterKernel);
				
				// ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("CopyBack"));
					// need ! if not using setBuffer to switch buffer.
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

		private void calcSize(int PNum/*, System.Action<uint> callback*/)
		{
			cs.GetKernelThreadGroupSizes(InBlockKernel, out uint x, out uint y ,out uint z);
			BlockNum   = (uint)(Mathf.CeilToInt((float)PNum / (x * y * z)));
			TotalNum   = BlockNum * x * y * z;
			counterNum = BlockNum * 16;	
		}
		
	}
}


// 1024 groups, 256 thread per groups
// --------------- 1024 group ----------------
// ---256 uint---		---256 uint---		 :
// 16			:  ...	16		     :	...	 :
// --------------		--------------		 :
// -------------------------------------------
