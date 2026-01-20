using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Seb.Helpers;
using TMPro;
using UnityEditor.UI;

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
		

		readonly Scan scan = new();
		readonly ComputeShader cs = ComputeHelper.LoadComputeShader("RadixSort");	
		
		ComputeBuffer sortedIndexBuffer;
		ComputeBuffer sortedKeyBuffer;

		ComputeBuffer BucketCounterBuffer;
		ComputeBuffer DstCounterBuffer;
		ComputeBuffer GlobalPSumBuffer;
		

		public void Run(ComputeBuffer itemsBuffer, ComputeBuffer keysBuffer, uint maxValue)
		{
			int count = itemsBuffer.count;			// ?? 
			
			int InBlockKernel  = cs.FindKernel("InBlockRadix");
			int OvBlockKernel  = cs.FindKernel("OvBlockRadix");
			int GScatterKernel = cs.FindKernel("GlobalScatter");

			// cs.SetInt(ID_NumInputs, count);			// how to deal with random size of data ?
			
			for (int i = 0; i < 8; i++)				// 8-pass for 32-bit uint
			{
				// ---- Init ----
				
				if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedIndexBuffer, count))	// count not right
				{
					cs.SetBuffer(GScatterKernel, ID_SortedIndex, sortedIndexBuffer);
				}

				if (ComputeHelper.CreateStructuredBuffer<uint>(ref sortedKeyBuffer, count))		// count not right
				{
					cs.SetBuffer(GScatterKernel, ID_SortedKeys, sortedKeyBuffer);
				}
				
				if (ComputeHelper.CreateStructuredBuffer<uint>(ref BucketCounterBuffer, count))	// count not right
				{
					cs.SetBuffer(InBlockKernel, ID_BucketCounter, BucketCounterBuffer);
					cs.SetBuffer(OvBlockKernel, ID_BucketCounter, BucketCounterBuffer);
				}

				if (ComputeHelper.CreateStructuredBuffer<uint>(ref DstCounterBuffer, count))	// count not right
				{
					cs.SetBuffer(OvBlockKernel, ID_DstCounter, DstCounterBuffer);
					cs.SetBuffer(GScatterKernel, ID_DstCounter, DstCounterBuffer);
				}

				if (ComputeHelper.CreateStructuredBuffer<uint>(ref GlobalPSumBuffer, count))	// count not right
				{
					cs.SetBuffer(InBlockKernel, ID_GlobalPSum, GlobalPSumBuffer);
					cs.SetBuffer(GScatterKernel, ID_GlobalPSum, GlobalPSumBuffer);
				}
				
				cs.SetBuffer(GScatterKernel ,ID_InputIndex, itemsBuffer);
				
				cs.SetBuffer(InBlockKernel ,ID_InputKeys, keysBuffer);
				cs.SetBuffer(OvBlockKernel ,ID_InputKeys, keysBuffer);
				cs.SetBuffer(GScatterKernel ,ID_InputKeys, keysBuffer);
				
				// run
				ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("InBlockRadix"));
				ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("OverBlockRadix"));
				ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("GlobalScatter"));
				// ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("CopyBack"));	// need or not?
				
				// switch buffer
				(sortedIndexBuffer, itemsBuffer) = (itemsBuffer, sortedIndexBuffer);
				(sortedKeyBuffer, keysBuffer) = (keysBuffer, sortedKeyBuffer);
			}
			// itemsBuffer, keysBuffer now is result.
		}

		public void Release()
		{
			ComputeHelper.Release(sortedIndexBuffer, sortedKeyBuffer, BucketCounterBuffer, DstCounterBuffer, GlobalPSumBuffer);
			scan.Release();
		}
	}
}