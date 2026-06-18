/*
 * License: Apache-2.0
 * Copyright 2026 Stefan Kalysta (stefan@redninjas.dev)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Godot;

namespace Vantix.Net;

/// <summary>Serialised fog-of-war PVS baked in the editor: a flat bitset of N² bits (N = TotalVoxels) where
/// bit (a*N + b) means voxel a sees b (symmetric). The server loads it at startup, so FoW works from tick 1.</summary>
[Tool]
[GlobalClass]
public partial class VoxelPvsData : Resource
{
	/// <summary>World-space min corner of the grid AABB. World <c>p</c> → voxel <c>floor((p - Origin) / VoxelSize)</c>.</summary>
	[Export] public Vector3 Origin { get; set; }
	/// <summary>Voxel edge length in metres, set at bake time.</summary>
	[Export] public float VoxelSize { get; set; } = 4.0f;
	/// <summary>Cells per axis. Total = X × Y × Z.</summary>
	[Export] public Vector3I Dims { get; set; }
	/// <summary>Packed visibility bit.</summary>
	[Export] public byte[] VisibilityBytes { get; set; }

	public int TotalVoxels => Dims.X * Dims.Y * Dims.Z;
	public bool HasData => VisibilityBytes != null && VisibilityBytes.Length > 0 && TotalVoxels > 0;

	/// <summary>Per voxel, how many it can see (including itself). O(N²) bit-scan — cache if queried repeatedly.</summary>
	public int[] ComputePerVoxelVisibleCounts()
	{
		if (!HasData) return System.Array.Empty<int>();
		int n = TotalVoxels;
		var counts = new int[n];
		for (int a = 0; a < n; a++)
		{
			long rowStart = (long)a * n;
			int count = 0;
			for (int b = 0; b < n; b++)
			{
				long bit = rowStart + b;
				if ((VisibilityBytes[bit >> 3] & (1 << (int)(bit & 7))) != 0) count++;
			}
			counts[a] = count;
		}
		return counts;
	}
}
