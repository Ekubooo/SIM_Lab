using Seb.GPUSorting;
using UnityEngine;
using Seb.Helpers.Internal;

namespace Seb.Helpers
{
	public class SHRadix
	{
		public ComputeBuffer SpatialKeys;
		public ComputeBuffer SpatialIndices;
		public ComputeBuffer SpatialOffsets;

		// readonly GPUCountSort gpuSort = new();	// old
		readonly GPURadixSort gpuSort = new();		// new
		readonly SpatialOffsetCalculator spatialOffsetsCalc = new();

		public SHRadix(int size)
		{
			CreateBuffers(size);
		}

		public void Resize(int newSize)
		{
			CreateBuffers(newSize);
		}

		// Before running: the SpatialKeys buffer should be populated with values.
		// After running: SpatialKeys will be sorted (in ascending order), and the Offsets buffer will
		// contain values for looking up the start index of each group of keys from the key value.
		// The Index buffer will contain the mapping of original unsorted keys to the sorted version.
		// ---- Example ----
		// Given SpatialKeys input of:          {6, 9, 2, 2, 6, 3, 9, 9, 3, 2}
		// This will be sorted to result in:    {2, 2, 2, 3, 6, 6, 9, 9, 9, 9}
		// Offsets will be: (x = irrelevant)    {x, x, 0, 3, x, x, 4, x, x, 6} 
		// So to look up where the '6' keys start for instance, Offsets[6] gives the answer (4).
		// Finally, the Index buffer will contain the mapping of original unsorted keys to the sorted version.
		// So, to sort any buffer in corresponding fashion, one can do: Sorted[i] = Unsorted[Indices[i]]
		
		// SpatialIndices BEFOR: 1, 2, 3... PNum
		// SpatialIndices AFTER: Order by SpatialKeys;
		// SpatialKeys	  BEFOR: Unorder
		// SpatialKeys	  AFTER: Order by SpatialKeys value;
		
		public void Run()
		{
			// gpuSort.Run(SpatialIndices, SpatialKeys, (uint)(SpatialKeys.count - 1));			// old
			gpuSort.Run(SpatialIndices, SpatialKeys);									// new 
			// now SpatialIndices, SpatialKeys are ordered.
			
			// change the name to avoid ambiguity
			spatialOffsetsCalc.Run(true, SpatialKeys, SpatialOffsets);
		}

		public void Release()
		{
			gpuSort.Release();
			ComputeHelper.Release(SpatialKeys, SpatialIndices, SpatialOffsets);
		}

		void CreateBuffers(int count)
		{
			ComputeHelper.CreateStructuredBuffer<uint>(ref SpatialKeys, count);
			ComputeHelper.CreateStructuredBuffer<uint>(ref SpatialIndices, count);
			ComputeHelper.CreateStructuredBuffer<uint>(ref SpatialOffsets, count);
		}
	}
}