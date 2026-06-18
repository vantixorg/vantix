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
using System.Collections.Generic;

namespace Vantix.Fx;

[Tool]
[GlobalClass]
public partial class GlassBaker : Node3D
{
	[Export] public MeshInstance3D Source { get; set; }
	[Export] public int SurfaceIndex { get; set; }
	[Export] public float Thickness { get; set; } = 0.015f;
	[Export] public float WeldEpsilon { get; set; } = 0.0008f;
	[Export] public float MinPaneArea { get; set; } = 0.02f;

	[Export] public bool Bake { get => false; set { if (value) RunBake(); } }
	[Export] public bool ClearGenerated { get => false; set { if (value) RemoveGenerated(); } }

	private sealed class Island
	{
		public readonly List<int> Tris = new();
		public Vector3 Normal;
	}

	private void RemoveGenerated()
	{
		foreach (var child in GetChildren())
			if (child is GlassPane)
				child.QueueFree();
	}

	private int[] BuildIndices(Godot.Collections.Array arrays, int vertexCount)
	{
		var idxVar = arrays[(int)Mesh.ArrayType.Index];
		if (idxVar.VariantType != Variant.Type.Nil)
			return idxVar.AsInt32Array();

		var seq = new int[vertexCount];
		for (int i = 0; i < vertexCount; i++) seq[i] = i;
		return seq;
	}

	private int[] WeldVertices(Vector3[] verts)
	{
		var map = new Dictionary<(long, long, long), int>();
		var rep = new int[verts.Length];
		float inv = 1f / Mathf.Max(WeldEpsilon, 1e-6f);
		for (int i = 0; i < verts.Length; i++)
		{
			var key = ((long)Mathf.Round(verts[i].X * inv), (long)Mathf.Round(verts[i].Y * inv), (long)Mathf.Round(verts[i].Z * inv));
			if (map.TryGetValue(key, out int r)) rep[i] = r;
			else { map[key] = i; rep[i] = i; }
		}
		return rep;
	}

	private static int Find(int[] parent, int i)
	{
		while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
		return i;
	}

	private static void Union(int[] parent, int a, int b)
	{
		int ra = Find(parent, a), rb = Find(parent, b);
		if (ra != rb) parent[ra] = rb;
	}

	private List<Island> SplitIslands(int[] indices, int[] rep, Vector3[] verts)
	{
		var parent = new int[verts.Length];
		for (int i = 0; i < parent.Length; i++) parent[i] = i;

		for (int i = 0; i + 2 < indices.Length; i += 3)
		{
			int a = rep[indices[i]], b = rep[indices[i + 1]], c = rep[indices[i + 2]];
			Union(parent, a, b);
			Union(parent, b, c);
		}

		var groups = new Dictionary<int, Island>();
		for (int i = 0; i + 2 < indices.Length; i += 3)
		{
			int root = Find(parent, rep[indices[i]]);
			if (!groups.TryGetValue(root, out var island))
				groups[root] = island = new Island();
			island.Tris.Add(i);
		}

		return new List<Island>(groups.Values);
	}

	private Vector3 ComputePlaneNormal(Island island, int[] indices, Vector3[] verts)
	{
		float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
		foreach (int t in island.Tris)
		{
			Vector3 p0 = verts[indices[t]], p1 = verts[indices[t + 1]], p2 = verts[indices[t + 2]];
			Vector3 m = (p1 - p0).Cross(p2 - p0);
			xx += m.X * m.X; xy += m.X * m.Y; xz += m.X * m.Z;
			yy += m.Y * m.Y; yz += m.Y * m.Z; zz += m.Z * m.Z;
		}

		Vector3 v = new Vector3(xx, yy, zz);
		v = v.LengthSquared() > 1e-20f ? v.Normalized() : Vector3.Up;
		for (int i = 0; i < 32; i++)
		{
			Vector3 mv = new Vector3(
				xx * v.X + xy * v.Y + xz * v.Z,
				xy * v.X + yy * v.Y + yz * v.Z,
				xz * v.X + yz * v.Y + zz * v.Z);
			float len = mv.Length();
			if (len < 1e-12f) break;
			v = mv / len;
		}
		return v.Normalized();
	}

	private List<int> ExtractOutlineLoop(Island island, int[] indices, int[] rep, Vector3[] verts, Vector3 normal)
	{
		float areaPos = 0f, areaNeg = 0f;
		foreach (int t in island.Tris)
		{
			Vector3 m = (verts[indices[t + 1]] - verts[indices[t]]).Cross(verts[indices[t + 2]] - verts[indices[t]]);
			if (m.Dot(normal) >= 0f) areaPos += m.Length(); else areaNeg += m.Length();
		}
		float sign = areaPos >= areaNeg ? 1f : -1f;

		var edgeCount = new Dictionary<(int, int), int>();
		foreach (int t in island.Tris)
		{
			Vector3 m = (verts[indices[t + 1]] - verts[indices[t]]).Cross(verts[indices[t + 2]] - verts[indices[t]]);
			float ml = m.Length();
			if (ml < 1e-12f || m.Dot(normal) * sign / ml < 0.3f) continue;
			for (int e = 0; e < 3; e++)
			{
				int a = rep[indices[t + e]], b = rep[indices[t + (e + 1) % 3]];
				var key = a < b ? (a, b) : (b, a);
				edgeCount[key] = edgeCount.GetValueOrDefault(key) + 1;
			}
		}

		var neighbors = new Dictionary<int, List<int>>();
		foreach (var (key, count) in edgeCount)
		{
			if (count != 1) continue;
			(int a, int b) = key;
			(neighbors.TryGetValue(a, out var la) ? la : neighbors[a] = new List<int>()).Add(b);
			(neighbors.TryGetValue(b, out var lb) ? lb : neighbors[b] = new List<int>()).Add(a);
		}
		if (neighbors.Count < 3) return null;

		List<int> best = null;
		var globalVisited = new HashSet<int>();
		foreach (int start in neighbors.Keys)
		{
			if (globalVisited.Contains(start)) continue;
			var loop = new List<int>();
			var visited = new HashSet<int>();
			int current = start, prev = -1;
			while (current != -1 && !visited.Contains(current))
			{
				loop.Add(current);
				visited.Add(current);
				globalVisited.Add(current);
				int next = -1;
				foreach (int n in neighbors[current])
					if (n != prev && !visited.Contains(n)) { next = n; break; }
				prev = current;
				current = next;
			}
			if (best == null || loop.Count > best.Count) best = loop;
		}

		return best != null && best.Count >= 3 ? best : null;
	}

	private bool BakeIsland(Island island, List<int> loop, Vector3[] verts)
	{
		Vector3 n = island.Normal.Normalized();
		if (n == Vector3.Zero) return false;

		Vector3 reference = Mathf.Abs(n.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
		Vector3 t = n.Cross(reference).Normalized();
		Vector3 b = n.Cross(t).Normalized();

		Vector3 centroid = Vector3.Zero;
		foreach (int v in loop) centroid += verts[v];
		centroid /= loop.Count;

		var pts2D = new Vector2[loop.Count];
		for (int i = 0; i < loop.Count; i++)
		{
			Vector3 rel = verts[loop[i]] - centroid;
			pts2D[i] = new Vector2(rel.Dot(t), rel.Dot(b));
		}

		Vector2 min = pts2D[0], max = pts2D[0];
		foreach (var p in pts2D)
		{
			min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
			max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
		}
		Vector2 size = max - min;
		if (size.X * size.Y < MinPaneArea) return false;

		Vector2 center2D = (min + max) * 0.5f;
		for (int i = 0; i < pts2D.Length; i++) pts2D[i] -= center2D;

		float signed = 0f;
		for (int i = 0; i < pts2D.Length; i++)
		{
			Vector2 p0 = pts2D[i], p1 = pts2D[(i + 1) % pts2D.Length];
			signed += p0.X * p1.Y - p1.X * p0.Y;
		}
		if (signed < 0f) System.Array.Reverse(pts2D);

		Vector3 originLocal = centroid + t * center2D.X + b * center2D.Y;
		var localTf = new Transform3D(new Basis(t, b, n), originLocal);

		var pane = new GlassPane
		{
			Name = $"GlassPane{GetChildCount()}",
			Outline = pts2D,
			Thickness = Thickness,
		};
		AddChild(pane);
		pane.Owner = GetTree().EditedSceneRoot;
		pane.GlobalTransform = Source.GlobalTransform * localTf;
		return true;
	}

	private void RunBake()
	{
		if (Source == null) { GD.PushWarning("GlassBaker: no Source MeshInstance3D set."); return; }
		var mesh = Source.Mesh;
		if (mesh == null) { GD.PushWarning("GlassBaker: Source has no mesh."); return; }
		if (SurfaceIndex < 0 || SurfaceIndex >= mesh.GetSurfaceCount())
		{
			GD.PushWarning($"GlassBaker: SurfaceIndex {SurfaceIndex} out of range (0..{mesh.GetSurfaceCount() - 1}).");
			return;
		}

		var arrays = mesh.SurfaceGetArrays(SurfaceIndex);
		var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
		if (verts.Length == 0) { GD.PushWarning("GlassBaker: surface has no vertices."); return; }

		var indices = BuildIndices(arrays, verts.Length);
		var rep = WeldVertices(verts);
		var islands = SplitIslands(indices, rep, verts);

		RemoveGenerated();
		int made = 0, noLoop = 0, tooSmall = 0;
		foreach (var island in islands)
		{
			island.Normal = ComputePlaneNormal(island, indices, verts);
			var loop = ExtractOutlineLoop(island, indices, rep, verts, island.Normal);
			if (loop == null) { noLoop++; continue; }
			if (BakeIsland(island, loop, verts)) made++; else tooSmall++;
		}

		GD.Print($"GlassBaker: baked {made} pane(s) from {islands.Count} island(s) — {noLoop} without outline, {tooSmall} below MinPaneArea.");
	}
}
