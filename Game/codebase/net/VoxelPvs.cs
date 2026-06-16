using Godot;

namespace Vantix.Server;

/// <summary>Server-side LoS precompute. Voxelises the map and bakes pairwise visibility so <see cref="CanSee"/>
/// is a bit lookup. Built incrementally; returns visible until <see cref="Built"/>. Optimistic: may over-reveal, never wrongly hides.</summary>
public class VoxelPvs
{
	/// <summary>Voxel cap for the runtime fallback build. Editor bakes pass a larger cap since they run offline.</summary>
	public const int DefaultMaxVoxels = 2500;
	/// <summary>Voxel cap for the editor bake. Kept below where per-bit-index arithmetic overflows int.MaxValue.</summary>
	public const int EditorBakeMaxVoxels = 16_000;

	public Vector3 Origin { get; private set; }
	public float VoxelSize { get; private set; }
	public Vector3I Dims { get; private set; }
	public int TotalVoxels => Dims.X * Dims.Y * Dims.Z;
	public bool Built { get; private set; }
	public bool IsBuilding => _visibility != null && !Built;
	public float BuildProgress01 => _buildN == 0 ? 0f : Mathf.Clamp((float)_buildNextA / _buildN, 0f, 1f);
	public long BuildRaysDone => _buildRayCount;

	private byte[] _visibility;
	private bool[] _solidVoxels;
	private PhysicsDirectSpaceState3D _buildSpace;
	private PhysicsRayQueryParameters3D _buildQuery;
	private PhysicsPointQueryParameters3D _buildPointQuery;
	private int _buildN;
	private long _buildNextA;
	private long _buildNextB;
	private long _buildRayCount;
	private int _buildSolidVoxels;
	private bool _buildCancelRequested;

	public int BuildSolidVoxels => _buildSolidVoxels;

	/// <summary>Starts a fresh build: sets up the grid (auto-coarsening voxelSize to stay under maxVoxels) and
	/// allocates the visibility buffer, but casts no rays — call <see cref="StepBuild"/> repeatedly for that.
	/// <see cref="CanSee"/> returns true (no culling) until <see cref="Built"/>.</summary>
	public void BeginBuild(PhysicsDirectSpaceState3D space, Aabb worldAabb, float voxelSize, uint collisionMask = 1u, int maxVoxels = DefaultMaxVoxels)
	{
		VoxelSize = Mathf.Max(0.5f, voxelSize);
		Origin = worldAabb.Position;
		for (;;)
		{
			Dims = new Vector3I(
				Mathf.Max(1, Mathf.CeilToInt(worldAabb.Size.X / VoxelSize)),
				Mathf.Max(1, Mathf.CeilToInt(worldAabb.Size.Y / VoxelSize)),
				Mathf.Max(1, Mathf.CeilToInt(worldAabb.Size.Z / VoxelSize)));
			if (TotalVoxels <= maxVoxels) break;
			VoxelSize *= 1.25f;
		}
		_buildN = TotalVoxels;
		long totalBits = (long)_buildN * _buildN;
		_visibility = new byte[(totalBits + 7) >> 3];
		_buildSpace = space;
		_buildQuery = new PhysicsRayQueryParameters3D
		{
			CollisionMask = collisionMask,
			CollideWithBodies = true,
			CollideWithAreas = false,
		};
		_buildPointQuery = new PhysicsPointQueryParameters3D
		{
			CollisionMask = collisionMask,
			CollideWithBodies = true,
			CollideWithAreas = false,
		};
		_buildNextA = 0;
		_buildNextB = 0;
		_buildRayCount = 0;
		_buildCancelRequested = false;
		PrecomputeSolidVoxels();
		Built = false;
	}

	/// <summary>Pre-pass flagging every voxel whose centre sits inside a collider on the layer mask.
	/// <see cref="StepBuild"/> skips all pairs involving them: no player stands inside solid, so those rays
	/// are wasted. Drops the ray count ~50-80% on dust2-scale maps. &lt;100ms at 16k voxels — point-overlap
	/// queries are far cheaper than the rays they replace.</summary>
	private void PrecomputeSolidVoxels()
	{
		_solidVoxels = new bool[_buildN];
		_buildSolidVoxels = 0;
		for (int i = 0; i < _buildN; i++)
		{
			_buildPointQuery.Position = VoxelCenter(i);
			var hits = _buildSpace.IntersectPoint(_buildPointQuery, maxResults: 1);
			if (hits.Count > 0) { _solidVoxels[i] = true; _buildSolidVoxels++; }
		}
	}

	/// <summary>Stops the active build at the next <see cref="StepBuild"/> call. The partial buffer is discarded;
	/// Built stays false, IsBuilding becomes false. Caller can restart or leave the PVS unbuilt (CanSee = true = no culling).</summary>
	public void CancelBuild()
	{
		if (!IsBuilding) return;
		_buildCancelRequested = true;
	}

	/// <summary>Casts up to <paramref name="maxRays"/> rays, returning true once the build completes (Built
	/// flips on the same call). Resumes exactly where the previous call stopped. Idempotent if built or not begun.</summary>
	public bool StepBuild(int maxRays)
	{
		if (Built) return true;
		if (_visibility == null) return false;
		if (_buildCancelRequested)
		{
			_visibility = null;
			_buildSpace = null;
			_buildQuery = null;
			_buildCancelRequested = false;
			Built = false;
			return false;
		}
		int n = _buildN;
		int rays = 0;
		for (long a = _buildNextA; a < n; a++)
		{
			bool aSolid = _solidVoxels != null && _solidVoxels[a];
			Vector3 from = aSolid ? Vector3.Zero : VoxelCenter((int)a);
			long bStart = (a == _buildNextA) ? _buildNextB : a;
			for (long b = bStart; b < n; b++)
			{
				if (aSolid || (_solidVoxels != null && _solidVoxels[b]))
				{
					continue;
				}
				bool visible;
				if (a == b)
				{
					visible = true;
				}
				else
				{
					if (rays >= maxRays)
					{
						_buildNextA = a;
						_buildNextB = b;
						return false;
					}
					Vector3 to = VoxelCenter((int)b);
					_buildQuery.From = from;
					_buildQuery.To = to;
					var hit = _buildSpace.IntersectRay(_buildQuery);
					visible = hit.Count == 0;
					rays++;
					_buildRayCount++;
				}
				if (visible)
				{
					SetBit(a * n + b);
					SetBit(b * n + a);
				}
			}
		}
		Built = true;
		_buildSpace = null;
		_buildQuery = null;
		_buildPointQuery = null;
		return true;
	}

	/// <summary>True if from/to have line-of-sight per the precomputed PVS. Out-of-bounds clamps to nearest voxel.
	/// While <see cref="Built"/> is false, returns true (no culling) so the game keeps playing until the PVS is ready.</summary>
	public bool CanSee(Vector3 from, Vector3 to)
	{
		if (!Built) return true;
		int a = WorldToIndex(from);
		int b = WorldToIndex(to);
		if (a < 0 || b < 0) return true;
		long bitIdx = (long)a * _buildN + b;
		return (_visibility[bitIdx >> 3] & (1 << (int)(bitIdx & 7))) != 0;
	}

	private void SetBit(long bitIdx)
	{
		_visibility[bitIdx >> 3] |= (byte)(1 << (int)(bitIdx & 7));
	}

	private int WorldToIndex(Vector3 world)
	{
		Vector3 local = (world - Origin) / VoxelSize;
		int x = Mathf.Clamp(Mathf.FloorToInt(local.X), 0, Dims.X - 1);
		int y = Mathf.Clamp(Mathf.FloorToInt(local.Y), 0, Dims.Y - 1);
		int z = Mathf.Clamp(Mathf.FloorToInt(local.Z), 0, Dims.Z - 1);
		return (z * Dims.Y + y) * Dims.X + x;
	}

	private Vector3 VoxelCenter(int index)
	{
		int x = index % Dims.X;
		int rem = index / Dims.X;
		int y = rem % Dims.Y;
		int z = rem / Dims.Y;
		return Origin + new Vector3(
			(x + 0.5f) * VoxelSize,
			(y + 0.5f) * VoxelSize,
			(z + 0.5f) * VoxelSize);
	}

	/// <summary>Counts set bits in the visibility buffer (~150ms at 32MB). Post-bake density log only, not a hot path.</summary>
	public long CountVisible()
	{
		if (_visibility == null) return 0;
		long count = 0;
		for (int i = 0; i < _visibility.Length; i++)
		{
			byte b = _visibility[i];
			while (b != 0) { count += b & 1; b >>= 1; }
		}
		return count;
	}

	/// <summary>Returns the visibility buffer for serialising into a <see cref="VoxelPvsData"/>. Caller may keep
	/// the reference — the next <see cref="BeginBuild"/> allocates a fresh buffer, so this array is then caller-owned.</summary>
	public byte[] ExportBitsAsBytes() => _visibility ?? System.Array.Empty<byte>();

	/// <summary>Adopts a baked <see cref="VoxelPvsData"/> by reference — no copy, formats match.
	/// Lets server startup skip the runtime build when the level was pre-baked.</summary>
	public void LoadFromData(VoxelPvsData data)
	{
		if (data == null || !data.HasData)
		{
			GD.PushWarning("[VoxelPvs] LoadFromData called with null/empty data — ignoring.");
			return;
		}
		Origin = data.Origin;
		VoxelSize = data.VoxelSize;
		Dims = data.Dims;
		_buildN = TotalVoxels;
		_visibility = data.VisibilityBytes;
		_buildSpace = null;
		_buildQuery = null;
		_buildRayCount = 0;
		_buildCancelRequested = false;
		Built = true;
	}

	/// <summary>Computes the playable AABB from CollisionShape3D nodes on the layer mask — so render-only
	/// geometry (skyboxes, decoration) with no collision is excluded. Falls back to a mesh walk when no
	/// collision shapes exist. Each axis is capped at <see cref="MaxAabbExtentM"/>.</summary>
	public static Aabb ComputeWorldAabb(Node root, uint layerMask = 1u)
	{
		Aabb result = default;
		bool any = false;
		WalkCollision(root, layerMask, ref result, ref any);
		if (!any) WalkMesh(root, ref result, ref any);
		if (!any) return new Aabb(Vector3.Zero, new Vector3(64f, 16f, 64f));
		result = result.Grow(2f);
		Vector3 size = result.Size;
		Vector3 center = result.Position + size * 0.5f;
		Vector3 cappedSize = new Vector3(
			Mathf.Min(size.X, MaxAabbExtentM),
			Mathf.Min(size.Y, MaxAabbExtentM),
			Mathf.Min(size.Z, MaxAabbExtentM));
		return new Aabb(center - cappedSize * 0.5f, cappedSize);
	}

	private const float MaxAabbExtentM = 256f;

	private static void WalkCollision(Node node, uint layerMask, ref Aabb acc, ref bool any)
	{
		if (node is CollisionShape3D cs && cs.Shape != null && !cs.Disabled)
		{
			var body = cs.GetParentOrNull<CollisionObject3D>();
			if (body != null && (body.CollisionLayer & layerMask) != 0)
			{
				var debugMesh = cs.Shape.GetDebugMesh();
				if (debugMesh != null)
				{
					Aabb local = debugMesh.GetAabb();
					Aabb world = cs.GlobalTransform * local;
					Vector3 sz = world.Size;
					if (sz.X < MaxAabbExtentM && sz.Y < MaxAabbExtentM && sz.Z < MaxAabbExtentM)
					{
						if (!any) { acc = world; any = true; }
						else acc = acc.Merge(world);
					}
				}
			}
		}
		foreach (var child in node.GetChildren())
			WalkCollision(child, layerMask, ref acc, ref any);
	}

	/// <summary>Diagnostic — walks the scene like <see cref="ComputeWorldAabb"/> and returns up to
	/// <paramref name="topN"/> colliders by max-axis extent, descending. Use it to find the out-of-world
	/// collider inflating an unexpectedly large AABB.</summary>
	public static string[] DescribeLargestColliders(Node root, uint layerMask = 1u, int topN = 10)
	{
		var found = new System.Collections.Generic.List<(string path, float maxExtent, Vector3 size, int layer)>();
		WalkCollect(root, layerMask, found);
		found.Sort((a, b) => b.maxExtent.CompareTo(a.maxExtent));
		int n = Mathf.Min(topN, found.Count);
		var result = new string[n];
		for (int i = 0; i < n; i++)
		{
			var (p, _, s, l) = found[i];
			result[i] = $"{p} | size=({s.X:F1},{s.Y:F1},{s.Z:F1})m | layer={l}";
		}
		return result;
	}

	private static void WalkCollect(Node node, uint layerMask, System.Collections.Generic.List<(string, float, Vector3, int)> sink)
	{
		if (node is CollisionShape3D cs && cs.Shape != null && !cs.Disabled)
		{
			var body = cs.GetParentOrNull<CollisionObject3D>();
			if (body != null && (body.CollisionLayer & layerMask) != 0)
			{
				var dm = cs.Shape.GetDebugMesh();
				if (dm != null)
				{
					Aabb local = dm.GetAabb();
					Aabb world = cs.GlobalTransform * local;
					Vector3 sz = world.Size;
					float maxExt = Mathf.Max(sz.X, Mathf.Max(sz.Y, sz.Z));
					sink.Add((cs.GetPath(), maxExt, sz, (int)body.CollisionLayer));
				}
			}
		}
		foreach (var child in node.GetChildren())
			WalkCollect(child, layerMask, sink);
	}

	private static void WalkMesh(Node node, ref Aabb acc, ref bool any)
	{
		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb local = mi.GetAabb();
			Aabb world = mi.GlobalTransform * local;
			Vector3 worldSize = world.Size;
			if (worldSize.X < MaxAabbExtentM && worldSize.Y < MaxAabbExtentM && worldSize.Z < MaxAabbExtentM)
			{
				if (!any) { acc = world; any = true; }
				else acc = acc.Merge(world);
			}
		}
		foreach (var child in node.GetChildren())
			WalkMesh(child, ref acc, ref any);
	}
}
