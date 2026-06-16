using Godot;

namespace Vantix.Net;

/// <summary>Serialisable FoW PVS data — output of <see cref="VoxelPvsInstance"/>'s editor bake. Ships as a
/// .tres beside the map .tscn; <see cref="NetServer"/> loads it via <see cref="VoxelPvs.LoadFromData"/> for FoW
/// from tick 1, no runtime build. Layout: flat <see cref="VisibilityBytes"/> of N² bits (N = <see cref="TotalVoxels"/>);
/// bit (a×N + b) set = voxel a sees b. Symmetric, so query order doesn't matter.</summary>
[Tool]
[GlobalClass]
public partial class VoxelPvsData : Resource
{
	/// <summary>World-space min corner of the grid AABB. World <c>p</c> → voxel <c>floor((p - Origin) / VoxelSize)</c>.</summary>
	[Export] public Vector3 Origin { get; set; }
	/// <summary>Cubic voxel edge length (m), as used at bake time (possibly auto-coarsened to fit the budget).</summary>
	[Export] public float VoxelSize { get; set; } = 4.0f;
	/// <summary>Cells per axis. Total = X × Y × Z.</summary>
	[Export] public Vector3I Dims { get; set; }
	/// <summary>Packed visibility bits. Length = ceil(N² / 8). Bit i = byte[i>>3] & (1 << (i & 7)).</summary>
	[Export] public byte[] VisibilityBytes { get; set; }

	public int TotalVoxels => Dims.X * Dims.Y * Dims.Z;
	public bool HasData => VisibilityBytes != null && VisibilityBytes.Length > 0 && TotalVoxels > 0;

	/// <summary>Per voxel, how many it can see (incl. itself). O(N²) bit-scan (~1s at N=16k) — cache if
	/// queried repeatedly. Feeds the density-heatmap gizmo.</summary>
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
