using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seb.Helpers;
using TMPro;
using UnityEditor.UI;

// todo: sort the input.
// todo: dispatch right number. (upper layer)
// todo: is PNum needed?
// todo: can GroupSize change?
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
		

		public void Run(ComputeBuffer indexBuffer, ComputeBuffer keysBuffer, uint maxValue)
		{
			int count = indexBuffer.count;			// ?? to be padding
													// if padding by hash kernel
													// then count == group size
			
			int InBlockKernel  = cs.FindKernel("InBlockRadix");
			int OvBlockKernel  = cs.FindKernel("OvBlockRadix");
			int GScatterKernel = cs.FindKernel("GlobalScatter");

			cs.SetInt(ID_numInputs, count);		
			
			// ---- Init ----
			// check the count again.

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedIndexBuffer, count))	
			{
				cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedKeyBuffer, count))		
			{
				cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer); 
			}
				
			if (ComputeHelper.CreateStructuredBuffer<uint>(ref BucketCounterBuffer, count))	
			{
				cs.SetBuffer(InBlockKernel, ID_BucketCounter, BucketCounterBuffer);
				cs.SetBuffer(OvBlockKernel, ID_BucketCounter, BucketCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref DstCounterBuffer, count))	
			{
				cs.SetBuffer(OvBlockKernel, ID_DstCounter, DstCounterBuffer);
				cs.SetBuffer(GScatterKernel, ID_DstCounter, DstCounterBuffer);
			}

			if (ComputeHelper.CreateStructuredBuffer<uint>(ref GlobalPSumBuffer, count))	
			{
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
				ComputeHelper.Dispatch(cs, count, kernelIndex: InBlockKernel);
				ComputeHelper.Dispatch(cs, count, kernelIndex: OvBlockKernel);
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
		
	}
}
