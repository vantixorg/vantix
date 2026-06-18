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
public partial class GlassPane : StaticBody3D
{
	[ExportGroup("Dimensions")]
	[Export] public float Width { get => _width; set { _width = value; OnExportChanged(); } }
	[Export] public float Height { get => _height; set { _height = value; OnExportChanged(); } }
	[Export(PropertyHint.Range, "0.003,0.1,0.001")] public float Thickness { get => _thickness; set { _thickness = value; OnExportChanged(); } }
	[Export] public Vector2[] Outline { get => _outline; set { _outline = value; OnExportChanged(); } }

	private float _width = 1.6f;
	private float _height = 2.2f;
	private float _thickness = 0.015f;
	private Vector2[] _outline;

	private bool _hasOutline;
	private Vector2[] _boundaryPoly;
	private Vector2 _boundsMin;
	private Vector2 _boundsMax;

	[ExportGroup("Fracture")]
	[Export(PropertyHint.Range, "0.08,0.4,0.01")] public float CellSize = 0.16f;
	[Export(PropertyHint.Range, "0.4,1,0.05")] public float CellJitter = 0.65f;

	[ExportGroup("Damage")]
	[Export] public float HoleRadius = 0.07f;
	[Export] public float CrackRadius = 0.34f;
	[Export(PropertyHint.Range, "0.004,0.05,0.001")] public float PenetrationPerHit = 0.012f;

	[ExportGroup("Dynamics")]
	[Export] public float PushSpeed = 4.5f;
	[Export] public float RadialSpeed = 2.5f;
	[Export] public float Lift = 0.8f;
	[Export] public float DistanceFalloff = 1.6f;
	[Export] public float SpinSpeed = 9.0f;
	[Export] public float WaveDuration = 0.2f;

	[ExportGroup("Performance")]
	[Export] public int MaxLiveShards = 64;
	[Export] public float ShardMinArea = 0.01f;
	[Export] public float LodDistance = 25.0f;
	[Export] public float ShardMergeDelay = 1.5f;

	[ExportGroup("Appearance")]
	[Export] public Color Tint { get => _tint; set { _tint = value; OnExportChanged(); } }
	[Export] public Color CrackColor { get => _crackColor; set { _crackColor = value; OnExportChanged(); } }
	[Export] public float CrackEmission { get => _crackEmission; set { _crackEmission = value; OnExportChanged(); } }
	[Export] public float CrackLineWidth { get => _crackLineWidth; set { _crackLineWidth = value; OnExportChanged(); } }

	private Color _tint = new(0.62f, 0.76f, 0.85f, 0.06f);
	private Color _crackColor = new(0.92f, 0.96f, 1.0f);
	private float _crackEmission = 2.5f;
	private float _crackLineWidth = 0.0035f;

	[ExportGroup("Frost & Dirt")]
	[Export(PropertyHint.Range, "0,1,0.01")] public float Frostness { get => _frostness; set { _frostness = value; OnExportChanged(); } }
	[Export] public Texture2D DirtTexture { get => _dirtTexture; set { _dirtTexture = value; OnExportChanged(); } }
	[Export(PropertyHint.Range, "0,2,0.05")] public float DirtAmount { get => _dirtAmount; set { _dirtAmount = value; OnExportChanged(); } }
	[Export] public Color DirtColor { get => _dirtColor; set { _dirtColor = value; OnExportChanged(); } }
	[Export] public Vector2 DirtTiling { get => _dirtTiling; set { _dirtTiling = value; OnExportChanged(); } }

	private float _frostness = 0.15f;
	private Texture2D _dirtTexture;
	private float _dirtAmount = 1.0f;
	private Color _dirtColor = new(0.28f, 0.27f, 0.24f);
	private Vector2 _dirtTiling = Vector2.One;

	[ExportGroup("Audio")]
	[Export] public Godot.Collections.Array<AudioStream> ImpactSounds { get; set; } = new();
	[Export] public float SoundMaxDistance = 30f;
	[Export(PropertyHint.Range, "-40,12,0.5")] public float SoundVolumeDb = 0f;

	private const int WorldLayer = 1;
	private const int DebrisLayer = 8;
	private const int GlassLayer = 32;
	private const int MaxImpacts = 16;

	private enum PaneState { Intact, Cracked }

	private class Cell
	{
		public Vector2[] Poly;
		public Vector2 Centroid;
		public float Area;
		public int Health;
		public bool Alive = true;
		public CollisionShape3D Col;
	}

	private MeshInstance3D _mesh;
	private CollisionShape3D _boxCollision;
	private ShaderMaterial _glassMaterial;
	private StandardMaterial3D _shardMaterial;
	private GpuParticles3D _dust;
	private ParticleProcessMaterial _dustProc;
	private MeshInstance3D _pile;
	private ArrayMesh _pileMesh;
	private readonly Vector4[] _impactBuffer = new Vector4[MaxImpacts];

	private PaneState _state = PaneState.Intact;
	private List<Cell> _cells;
	private List<int>[] _adjacency;
	private float[] _frameSupport;
	private readonly List<Vector2> _impacts = new();
	private readonly List<RigidBody3D> _shards = new();
	private readonly List<(Vector3 pos, Vector3 vel)> _pendingDust = new();
	private int _liveShardEstimate;
	private bool _lodFar;
	private double _mergeAccum;
	private AudioStreamPlayer3D[] _audioPool;
	private int _audioCursor;
	private const int AudioPoolSize = 6;

	private ShaderMaterial BuildGlassMaterial()
	{
		var shader = GD.Load<Shader>("res://shaders/glass_crack.gdshader");
		var mat = new ShaderMaterial { Shader = shader };
		mat.SetShaderParameter("impact_count", 0);
		mat.SetShaderParameter("impacts", _impactBuffer);
		return mat;
	}

	private void RecomputeBoundary()
	{
		if (_outline != null && _outline.Length >= 3)
		{
			_boundaryPoly = _outline;
			_hasOutline = true;
		}
		else
		{
			float hw = _width * 0.5f;
			float hh = _height * 0.5f;
			_boundaryPoly = new[] { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) };
			_hasOutline = false;
		}

		_boundsMin = _boundaryPoly[0];
		_boundsMax = _boundaryPoly[0];
		foreach (var v in _boundaryPoly)
		{
			_boundsMin = new Vector2(Mathf.Min(_boundsMin.X, v.X), Mathf.Min(_boundsMin.Y, v.Y));
			_boundsMax = new Vector2(Mathf.Max(_boundsMax.X, v.X), Mathf.Max(_boundsMax.Y, v.Y));
		}
	}

	private Vector2 PaneSize() => _boundsMax - _boundsMin;

	private bool IsOnBoundary(Vector2 p)
	{
		var poly = _boundaryPoly;
		int n = poly.Length;
		const float eps = 1.5e-3f;
		for (int i = 0; i < n; i++)
			if (DistanceToSegment(p, poly[i], poly[(i + 1) % n]) <= eps)
				return true;
		return false;
	}

	private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float len2 = ab.LengthSquared();
		float t = len2 > 1e-12f ? Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f) : 0f;
		return p.DistanceTo(a + ab * t);
	}

	private void ApplyGlassParams()
	{
		if (_glassMaterial == null) return;
		_glassMaterial.SetShaderParameter("pane_size", PaneSize());
		_glassMaterial.SetShaderParameter("tint", Tint);
		_glassMaterial.SetShaderParameter("crack_color", new Color(CrackColor.R, CrackColor.G, CrackColor.B));
		_glassMaterial.SetShaderParameter("crack_emission", CrackEmission);
		_glassMaterial.SetShaderParameter("line_width", CrackLineWidth);
		_glassMaterial.SetShaderParameter("frostedness", Frostness);
		_glassMaterial.SetShaderParameter("dirt_amount", DirtAmount);
		_glassMaterial.SetShaderParameter("dirt_color", new Color(DirtColor.R, DirtColor.G, DirtColor.B));
		_glassMaterial.SetShaderParameter("dirt_tiling", DirtTiling);
		if (_dirtTexture != null) _glassMaterial.SetShaderParameter("dirt_texture", _dirtTexture);
	}

	private void EnsureVisual()
	{
		_glassMaterial ??= BuildGlassMaterial();
		_shardMaterial ??= BuildShardMaterial();
		if (_mesh == null)
		{
			_mesh = GetNodeOrNull<MeshInstance3D>("GlassVisual");
			if (_mesh == null)
			{
				_mesh = new MeshInstance3D { Name = "GlassVisual" };
				AddChild(_mesh);
			}
			_mesh.MaterialOverride = _glassMaterial;
		}
	}

	private void RebuildVisual()
	{
		RecomputeBoundary();
		EnsureVisual();
		ApplyGlassParams();
		_mesh.Mesh = BuildIntactMesh();
		EnsureCollision();
	}

	private void EnsureCollision()
	{
		if (_boxCollision == null)
		{
			_boxCollision = GetNodeOrNull<CollisionShape3D>("GlassCollision");
			if (_boxCollision == null)
			{
				_boxCollision = new CollisionShape3D { Name = "GlassCollision" };
				AddChild(_boxCollision);
			}
		}

		if (_mesh?.Mesh is ArrayMesh am && am.GetSurfaceCount() > 0)
			_boxCollision.Shape = am.CreateTrimeshShape();
		else
			_boxCollision.Shape = new BoxShape3D { Size = new Vector3(Width, Height, Mathf.Max(Thickness, 0.003f)) };

		if (Engine.IsEditorHint())
			UpdateConfigurationWarnings();
	}

	private void OnExportChanged()
	{
		if (Engine.IsEditorHint() && IsInsideTree())
			RebuildVisual();
	}

	private StandardMaterial3D BuildShardMaterial()
	{
		return new StandardMaterial3D
		{
			AlbedoColor = Tint,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			Metallic = 0.0f,
			MetallicSpecular = 0.9f,
			Roughness = 0.04f,
			RefractionEnabled = true,
			RefractionScale = 0.03f,
			RimEnabled = true,
			Rim = 0.5f,
			RimTint = 0.3f,
			ClearcoatEnabled = true,
			Clearcoat = 0.7f,
			ClearcoatRoughness = 0.04f,
		};
	}

	private Mesh BuildIntactMesh()
	{
		float halfT = Mathf.Max(Thickness, 0.003f) * 0.5f;
		var poly = _boundaryPoly;
		int n = poly.Length;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		int[] tris = Geometry2D.TriangulatePolygon(poly);
		if (tris == null || tris.Length < 3)
		{
			tris = new int[(n - 2) * 3];
			for (int i = 0; i < n - 2; i++)
			{
				tris[i * 3] = 0;
				tris[i * 3 + 1] = i + 1;
				tris[i * 3 + 2] = i + 2;
			}
		}

		st.SetNormal(Vector3.Back);
		for (int i = 0; i < tris.Length; i += 3)
		{
			AddFaceVertex(st, poly[tris[i]], halfT);
			AddFaceVertex(st, poly[tris[i + 1]], halfT);
			AddFaceVertex(st, poly[tris[i + 2]], halfT);
		}

		st.SetNormal(Vector3.Forward);
		for (int i = 0; i < tris.Length; i += 3)
		{
			AddFaceVertex(st, poly[tris[i]], -halfT);
			AddFaceVertex(st, poly[tris[i + 2]], -halfT);
			AddFaceVertex(st, poly[tris[i + 1]], -halfT);
		}

		for (int i = 0; i < n; i++)
		{
			Vector2 p0 = poly[i], p1 = poly[(i + 1) % n];
			Vector2 nrm = new Vector2(p1.Y - p0.Y, p0.X - p1.X).Normalized();
			st.SetNormal(new Vector3(nrm.X, nrm.Y, 0f));
			Vector3 f0 = new(p0.X, p0.Y, halfT), b0 = new(p0.X, p0.Y, -halfT);
			Vector3 f1 = new(p1.X, p1.Y, halfT), b1 = new(p1.X, p1.Y, -halfT);
			st.SetUV(Uv(p0)); st.AddVertex(f0);
			st.SetUV(Uv(p0)); st.AddVertex(b0);
			st.SetUV(Uv(p1)); st.AddVertex(b1);
			st.SetUV(Uv(p0)); st.AddVertex(f0);
			st.SetUV(Uv(p1)); st.AddVertex(b1);
			st.SetUV(Uv(p1)); st.AddVertex(f1);
		}

		return st.Commit();
	}

	private void AddFaceVertex(SurfaceTool st, Vector2 p, float z)
	{
		st.SetUV(Uv(p));
		st.AddVertex(new Vector3(p.X, p.Y, z));
	}

	private List<Vector2> BuildSeeds(Vector2 impact, RandomNumberGenerator rng)
	{
		var seeds = new List<Vector2>();
		Vector2 size = PaneSize();

		int nx = Mathf.Max(2, Mathf.RoundToInt(size.X / CellSize));
		int ny = Mathf.Max(2, Mathf.RoundToInt(size.Y / CellSize));
		float cw = size.X / nx;
		float ch = size.Y / ny;
		for (int i = 0; i < nx; i++)
			for (int j = 0; j < ny; j++)
			{
				float x = _boundsMin.X + (i + 0.5f) * cw + rng.RandfRange(-CellJitter, CellJitter) * cw * 0.5f;
				float y = _boundsMin.Y + (j + 0.5f) * ch + rng.RandfRange(-CellJitter, CellJitter) * ch * 0.5f;
				Vector2 p = new(Mathf.Clamp(x, _boundsMin.X, _boundsMax.X), Mathf.Clamp(y, _boundsMin.Y, _boundsMax.Y));
				if (_hasOutline && !Geometry2D.IsPointInPolygon(p, _boundaryPoly)) continue;
				seeds.Add(p);
			}

		Vector2 center = new(Mathf.Clamp(impact.X, _boundsMin.X, _boundsMax.X), Mathf.Clamp(impact.Y, _boundsMin.Y, _boundsMax.Y));
		int fine = 7;
		float baseAng = rng.RandfRange(0f, Mathf.Tau);
		for (int k = 0; k < fine; k++)
		{
			float ang = baseAng + Mathf.Tau * k / fine;
			float r = rng.RandfRange(0.025f, 0.085f);
			Vector2 p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
			seeds.Add(new Vector2(Mathf.Clamp(p.X, _boundsMin.X, _boundsMax.X), Mathf.Clamp(p.Y, _boundsMin.Y, _boundsMax.Y)));
		}
		seeds.Add(center);

		return seeds;
	}

	private static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 anchor, Vector2 normal)
	{
		var result = new List<Vector2>(poly.Count + 2);
		int n = poly.Count;
		for (int i = 0; i < n; i++)
		{
			Vector2 a = poly[i];
			Vector2 b = poly[(i + 1) % n];
			float da = (a - anchor).Dot(normal);
			float db = (b - anchor).Dot(normal);
			bool insideA = da <= 0f;
			bool insideB = db <= 0f;
			if (insideA) result.Add(a);
			if (insideA != insideB)
			{
				float denom = da - db;
				float tt = Mathf.Abs(denom) > 1e-9f ? da / denom : 0f;
				result.Add(a + (b - a) * tt);
			}
		}
		return result;
	}

	private List<Vector2> BuildCellPoly(int index, List<Vector2> seeds)
	{
		var poly = new List<Vector2>
		{
			new(_boundsMin.X, _boundsMin.Y), new(_boundsMax.X, _boundsMin.Y),
			new(_boundsMax.X, _boundsMax.Y), new(_boundsMin.X, _boundsMax.Y),
		};

		Vector2 si = seeds[index];
		for (int j = 0; j < seeds.Count && poly.Count >= 3; j++)
		{
			if (j == index) continue;
			Vector2 sj = seeds[j];
			Vector2 normal = sj - si;
			if (normal.LengthSquared() < 1e-10f) continue;
			Vector2 anchor = (si + sj) * 0.5f;
			poly = ClipHalfPlane(poly, anchor, normal);
		}

		if (!_hasOutline || poly.Count < 3) return poly;

		var clipped = Geometry2D.IntersectPolygons(poly.ToArray(), _boundaryPoly);
		List<Vector2> best = null;
		float bestArea = 0f;
		foreach (Vector2[] piece in clipped)
		{
			if (piece.Length < 3) continue;
			float area = PolygonArea(piece);
			if (area > bestArea) { bestArea = area; best = new List<Vector2>(piece); }
		}
		return best ?? new List<Vector2>();
	}

	private static float PolygonArea(IReadOnlyList<Vector2> poly)
	{
		float area = 0f;
		int n = poly.Count;
		for (int i = 0; i < n; i++)
		{
			Vector2 a = poly[i];
			Vector2 b = poly[(i + 1) % n];
			area += a.X * b.Y - b.X * a.Y;
		}
		return Mathf.Abs(area) * 0.5f;
	}

	private int PenetrationCost()
	{
		return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Thickness, 0.003f) / PenetrationPerHit));
	}

	private static (int, int, int, int) EdgeKey(Vector2 p0, Vector2 p1)
	{
		int ax = Mathf.RoundToInt(p0.X * 2000f), ay = Mathf.RoundToInt(p0.Y * 2000f);
		int bx = Mathf.RoundToInt(p1.X * 2000f), by = Mathf.RoundToInt(p1.Y * 2000f);
		return (ax < bx || (ax == bx && ay <= by)) ? (ax, ay, bx, by) : (bx, by, ax, ay);
	}

	private void Fracture(Vector2 impactLocal, RandomNumberGenerator rng)
	{
		var cam = GetViewport()?.GetCamera3D();
		_lodFar = cam != null && cam.GlobalPosition.DistanceTo(GlobalPosition) > LodDistance;

		var seeds = BuildSeeds(impactLocal, rng);
		_cells = new List<Cell>(seeds.Count);
		Vector2 size = PaneSize();
		float minArea = size.X * size.Y * 1e-4f;
		float halfT = Mathf.Max(Thickness, 0.003f) * 0.5f;

		if (_boxCollision != null) _boxCollision.Disabled = true;

		for (int i = 0; i < seeds.Count; i++)
		{
			var poly = BuildCellPoly(i, seeds);
			if (poly.Count < 3) continue;
			float area = PolygonArea(poly);
			if (area < minArea) continue;

			Vector2 centroid = Vector2.Zero;
			foreach (var v in poly) centroid += v;
			centroid /= poly.Count;

			var cell = new Cell { Poly = poly.ToArray(), Centroid = centroid, Area = area, Health = PenetrationCost() };

			var hull = new Vector3[cell.Poly.Length * 2];
			for (int k = 0; k < cell.Poly.Length; k++)
			{
				Vector3 p = new(cell.Poly[k].X, cell.Poly[k].Y, 0f);
				hull[k * 2] = p + Vector3.Back * halfT;
				hull[k * 2 + 1] = p + Vector3.Forward * halfT;
			}
			cell.Col = new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = hull } };
			AddChild(cell.Col);

			_cells.Add(cell);
		}

		BuildAdjacency();
		_mesh.Mesh = BuildStandingMesh();
		_state = PaneState.Cracked;
	}

	private void BuildAdjacency()
	{
		int n = _cells.Count;

		_adjacency = new List<int>[n];
		_frameSupport = new float[n];
		for (int i = 0; i < n; i++) _adjacency[i] = new List<int>();

		var first = new Dictionary<(int, int, int, int), int>();

		for (int ci = 0; ci < n; ci++)
		{
			var poly = _cells[ci].Poly;
			int m = poly.Length;
			for (int i = 0; i < m; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % m];
				var key = EdgeKey(p0, p1);
				if (first.TryGetValue(key, out int cj))
				{
					_adjacency[ci].Add(cj);
					_adjacency[cj].Add(ci);
				}
				else
				{
					first[key] = ci;
					if (IsOnBoundary((p0 + p1) * 0.5f)) _frameSupport[ci] = 1f;
				}
			}
		}
	}

	private bool SolveSupport(Vector3 worldImpact, Vector3 worldDir, RandomNumberGenerator rng)
	{
		if (_adjacency == null) return false;
		int n = _cells.Count;
		Vector3 impactLocal3 = ToLocal(worldImpact);
		Vector2 impactLocal = new(impactLocal3.X, impactLocal3.Y);

		var reached = new bool[n];
		var queue = new Queue<int>();
		for (int i = 0; i < n; i++)
			if (_cells[i].Alive && _frameSupport[i] > 0f)
			{
				reached[i] = true;
				queue.Enqueue(i);
			}

		while (queue.Count > 0)
		{
			int c = queue.Dequeue();
			foreach (int nb in _adjacency[c])
				if (_cells[nb].Alive && !reached[nb])
				{
					reached[nb] = true;
					queue.Enqueue(nb);
				}
		}

		bool fell = false;
		for (int i = 0; i < n; i++)
			if (_cells[i].Alive && !reached[i])
			{
				float dist = _cells[i].Centroid.DistanceTo(impactLocal);
				float delay = Mathf.Clamp(dist, 0f, 1.5f) / 1.5f * WaveDuration;
				KnockOut(_cells[i], worldImpact, worldDir, rng, delay);
				fell = true;
			}

		return fell;
	}

	private Mesh BuildStandingMesh()
	{
		float halfT = Mathf.Max(Thickness, 0.003f) * 0.5f;

		var edgeCount = new Dictionary<(int, int, int, int), int>();
		foreach (var cell in _cells)
		{
			if (!cell.Alive) continue;
			var poly = cell.Poly;
			for (int i = 0; i < poly.Length; i++)
			{
				var key = EdgeKey(poly[i], poly[(i + 1) % poly.Length]);
				edgeCount[key] = edgeCount.GetValueOrDefault(key) + 1;
			}
		}

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		bool any = false;

		foreach (var cell in _cells)
		{
			if (!cell.Alive) continue;
			any = true;
			var poly = cell.Poly;
			int n = poly.Length;
			Vector2 cc = cell.Centroid;
			Vector2 cuv = Uv(cc);

			st.SetNormal(Vector3.Back);
			for (int i = 0; i < n; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % n];
				st.SetUV(cuv); st.AddVertex(new Vector3(cc.X, cc.Y, halfT));
				st.SetUV(Uv(p0)); st.AddVertex(new Vector3(p0.X, p0.Y, halfT));
				st.SetUV(Uv(p1)); st.AddVertex(new Vector3(p1.X, p1.Y, halfT));
			}

			st.SetNormal(Vector3.Forward);
			for (int i = 0; i < n; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % n];
				st.SetUV(cuv); st.AddVertex(new Vector3(cc.X, cc.Y, -halfT));
				st.SetUV(Uv(p1)); st.AddVertex(new Vector3(p1.X, p1.Y, -halfT));
				st.SetUV(Uv(p0)); st.AddVertex(new Vector3(p0.X, p0.Y, -halfT));
			}

			for (int i = 0; i < n; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % n];
				if (edgeCount.GetValueOrDefault(EdgeKey(p0, p1)) != 1) continue;
				Vector2 e = p1 - p0;
				Vector2 nrm = new Vector2(e.Y, -e.X).Normalized();
				if (nrm.Dot((p0 + p1) * 0.5f - cc) < 0f) nrm = -nrm;
				st.SetNormal(new Vector3(nrm.X, nrm.Y, 0f));
				Vector3 f0 = new(p0.X, p0.Y, halfT), b0 = new(p0.X, p0.Y, -halfT);
				Vector3 f1 = new(p1.X, p1.Y, halfT), b1 = new(p1.X, p1.Y, -halfT);
				st.SetUV(Uv(p0)); st.AddVertex(f0);
				st.SetUV(Uv(p0)); st.AddVertex(b0);
				st.SetUV(Uv(p1)); st.AddVertex(b1);
				st.SetUV(Uv(p0)); st.AddVertex(f0);
				st.SetUV(Uv(p1)); st.AddVertex(b1);
				st.SetUV(Uv(p1)); st.AddVertex(f1);
			}
		}

		return any ? st.Commit() : new ArrayMesh();
	}

	private Vector2 Uv(Vector2 p)
	{
		Vector2 s = PaneSize();
		return new Vector2((p.X - _boundsMin.X) / s.X, (p.Y - _boundsMin.Y) / s.Y);
	}

	private void UpdateCracks()
	{
		int n = _impacts.Count;
		for (int i = 0; i < MaxImpacts; i++)
			_impactBuffer[i] = i < n
				? new Vector4(_impacts[i].X, _impacts[i].Y, CrackRadius, 1f)
				: Vector4.Zero;
		_glassMaterial.SetShaderParameter("impacts", _impactBuffer);
		_glassMaterial.SetShaderParameter("impact_count", n);
	}

	private Mesh BuildShardMesh(Cell cell, float halfT, out Vector3[] hull)
	{
		var poly = cell.Poly;
		int n = poly.Length;
		Vector2 c2 = cell.Centroid;

		var front = new Vector3[n];
		var back = new Vector3[n];
		hull = new Vector3[n * 2];
		for (int i = 0; i < n; i++)
		{
			Vector2 lp = poly[i] - c2;
			front[i] = new Vector3(lp.X, lp.Y, halfT);
			back[i] = new Vector3(lp.X, lp.Y, -halfT);
			hull[i * 2] = front[i];
			hull[i * 2 + 1] = back[i];
		}

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Vector3 fc = new(0f, 0f, halfT);
		Vector3 bc = new(0f, 0f, -halfT);
		st.SetNormal(Vector3.Back);
		for (int i = 0; i < n; i++)
		{
			st.AddVertex(fc); st.AddVertex(front[i]); st.AddVertex(front[(i + 1) % n]);
		}
		st.SetNormal(Vector3.Forward);
		for (int i = 0; i < n; i++)
		{
			st.AddVertex(bc); st.AddVertex(back[(i + 1) % n]); st.AddVertex(back[i]);
		}

		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			Vector2 e = poly[j] - poly[i];
			Vector2 nrm2 = new(e.Y, -e.X);
			Vector2 mid = (poly[i] + poly[j]) * 0.5f;
			if (nrm2.Dot(mid - c2) < 0f) nrm2 = -nrm2;
			nrm2 = nrm2.Normalized();
			st.SetNormal(new Vector3(nrm2.X, nrm2.Y, 0f));
			st.AddVertex(front[i]); st.AddVertex(back[i]); st.AddVertex(back[j]);
			st.AddVertex(front[i]); st.AddVertex(back[j]); st.AddVertex(front[j]);
		}

		return st.Commit();
	}

	private void KnockOut(Cell cell, Vector3 worldImpact, Vector3 worldDir, RandomNumberGenerator rng, float delay)
	{
		cell.Alive = false;
		if (cell.Col != null) { cell.Col.QueueFree(); cell.Col = null; }

		Vector3 center3 = new(cell.Centroid.X, cell.Centroid.Y, 0f);
		Vector3 worldCenter = GlobalTransform * center3;

		Vector3 radial = worldCenter - worldImpact;
		float dist = radial.Length();
		Vector3 radialDir = dist > 1e-3f ? radial / dist : worldDir;
		float falloff = 1f / (1f + dist * DistanceFalloff);
		Vector3 jitter = new(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f));
		Vector3 velocity = worldDir.Normalized() * PushSpeed * falloff
			+ radialDir * RadialSpeed * falloff
			+ Vector3.Up * Lift * falloff
			+ jitter * 0.5f;
		Vector3 spin = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)) * SpinSpeed;

		bool asBody = !_lodFar && cell.Area >= ShardMinArea && _liveShardEstimate < MaxLiveShards;
		if (asBody)
		{
			_liveShardEstimate++;
			SpawnShard(cell, worldCenter, velocity, spin, delay);
		}
		else
		{
			_pendingDust.Add((worldCenter, velocity));
		}
	}

	private void SpawnShard(Cell cell, Vector3 worldCenter, Vector3 velocity, Vector3 spin, float delay)
	{
		float halfT = Mathf.Max(Thickness, 0.003f) * 0.5f;
		var timer = GetTree().CreateTimer(delay);
		timer.Timeout += () =>
		{
			if (!IsInsideTree()) return;
			var mesh = BuildShardMesh(cell, halfT, out var hull);
			if (mesh.GetSurfaceCount() == 0) return;

			var body = new RigidBody3D
			{
				CollisionLayer = DebrisLayer,
				CollisionMask = WorldLayer,
				Mass = 0.2f,
				ContinuousCd = true,
				TopLevel = true,
			};
			body.AddChild(new MeshInstance3D { Name = "M", Mesh = mesh, MaterialOverride = _shardMaterial });
			body.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = hull } });

			AddChild(body);
			body.GlobalTransform = new Transform3D(GlobalTransform.Basis, worldCenter);
			body.LinearVelocity = velocity;
			body.AngularVelocity = spin;
			_shards.Add(body);
		};
	}

	private void EnsureDust()
	{
		if (_dust != null) return;
		_dustProc = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 90f,
			Gravity = new Vector3(0f, -9f, 0f),
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 2.5f,
			ScaleMin = 0.3f,
			ScaleMax = 1.0f,
			DampingMin = 0.5f,
			DampingMax = 1.5f,
		};
		var quad = new QuadMesh { Size = new Vector2(0.03f, 0.03f) };
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(Tint.R + 0.3f, Tint.G + 0.3f, Tint.B + 0.35f, 0.9f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			DisableReceiveShadows = true,
		};
		_dust = new GpuParticles3D
		{
			Name = "GlassDust",
			TopLevel = true,
			OneShot = true,
			Explosiveness = 1f,
			Emitting = false,
			Lifetime = 1.6,
			Amount = 256,
			ProcessMaterial = _dustProc,
			DrawPass1 = quad,
			MaterialOverride = mat,
		};
		AddChild(_dust);
	}

	private void FlushDust()
	{
		if (_pendingDust.Count == 0) return;
		EnsureDust();

		Vector3 min = _pendingDust[0].pos, max = _pendingDust[0].pos;
		foreach (var (pos, _) in _pendingDust)
		{
			min = new Vector3(Mathf.Min(min.X, pos.X), Mathf.Min(min.Y, pos.Y), Mathf.Min(min.Z, pos.Z));
			max = new Vector3(Mathf.Max(max.X, pos.X), Mathf.Max(max.Y, pos.Y), Mathf.Max(max.Z, pos.Z));
		}
		Vector3 center = (min + max) * 0.5f;
		Vector3 ext = (max - min) * 0.5f + Vector3.One * 0.05f;

		_dustProc.EmissionBoxExtents = ext;
		_dust.Amount = Mathf.Clamp(_pendingDust.Count * 3, 12, 512);
		_dust.GlobalPosition = center;
		_dust.Restart();
		_pendingDust.Clear();
	}

	private void MergeSettledShards()
	{
		List<RigidBody3D> settled = null;
		foreach (var body in _shards)
			if (GodotObject.IsInstanceValid(body) && body.Sleeping)
				(settled ??= new List<RigidBody3D>()).Add(body);
		if (settled == null) return;

		if (_pile == null)
		{
			_pile = new MeshInstance3D { Name = "GlassPile", TopLevel = true, MaterialOverride = _shardMaterial };
			AddChild(_pile);
		}

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		if (_pileMesh != null) st.AppendFrom(_pileMesh, 0, Transform3D.Identity);
		foreach (var body in settled)
		{
			var mi = body.GetNodeOrNull<MeshInstance3D>("M");
			if (mi?.Mesh != null) st.AppendFrom(mi.Mesh, 0, body.GlobalTransform);
			_shards.Remove(body);
			_liveShardEstimate = Mathf.Max(0, _liveShardEstimate - 1);
			body.QueueFree();
		}

		_pileMesh = st.Commit();
		_pile.Mesh = _pileMesh;
	}

	private void EnsureAudio()
	{
		if (_audioPool != null) return;
		_audioPool = new AudioStreamPlayer3D[AudioPoolSize];
		for (int i = 0; i < AudioPoolSize; i++)
		{
			var p = new AudioStreamPlayer3D();
			AddChild(p);
			_audioPool[i] = p;
		}
	}

	private void PlayImpactSound(Vector3 worldPos, int seed)
	{
		if (Engine.IsEditorHint() || ImpactSounds == null || ImpactSounds.Count == 0) return;
		var stream = ImpactSounds[(seed & 0x7fffffff) % ImpactSounds.Count];
		if (stream == null) return;
		EnsureAudio();
		var p = _audioPool[_audioCursor];
		_audioCursor = (_audioCursor + 1) % _audioPool.Length;
		p.Stream = stream;
		p.GlobalPosition = worldPos;
		p.VolumeDb = SoundVolumeDb;
		p.MaxDistance = SoundMaxDistance;
		p.Play();
	}

	public override void _Ready()
	{
		RebuildVisual();
		if (Engine.IsEditorHint()) return;

		AddToGroup("glass");
		CollisionLayer = GlassLayer;
		CollisionMask = 0;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _shards.Count == 0) return;
		_mergeAccum += delta;
		if (_mergeAccum < ShardMergeDelay) return;
		_mergeAccum = 0;
		MergeSettledShards();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void NetShatter(Vector3 worldImpact, Vector3 worldDir, int seed)
	{
		Hit(worldImpact, worldDir, seed);
	}

	public void RequestShatter(Vector3 worldImpact, Vector3 worldDir)
	{
		int seed = (int)GD.Randi();
		var mp = Multiplayer;
		if (mp != null && mp.HasMultiplayerPeer())
			Rpc(MethodName.NetShatter, worldImpact, worldDir, seed);
		else
			Hit(worldImpact, worldDir, seed);
	}

	public void Hit(Vector3 worldImpact, Vector3 worldDir, int seed)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)seed };
		Vector3 impactLocal3 = ToLocal(worldImpact);
		Vector2 impactLocal = new(impactLocal3.X, impactLocal3.Y);

		PlayImpactSound(worldImpact, seed);

		if (_state == PaneState.Intact)
			Fracture(impactLocal, rng);

		_impacts.Add(impactLocal);
		if (_impacts.Count > MaxImpacts) _impacts.RemoveAt(0);
		UpdateCracks();

		float r2 = HoleRadius * HoleRadius;
		bool changed = false;
		Cell nearest = null;
		float nearestSq = float.MaxValue;
		foreach (var cell in _cells)
		{
			if (!cell.Alive) continue;
			float dsq = cell.Centroid.DistanceSquaredTo(impactLocal);
			if (dsq < nearestSq) { nearestSq = dsq; nearest = cell; }
			if (dsq <= r2 && --cell.Health <= 0)
			{
				KnockOut(cell, worldImpact, worldDir, rng, 0f);
				changed = true;
			}
		}
		if (nearest != null && nearest.Alive && nearestSq > r2 && --nearest.Health <= 0)
		{
			KnockOut(nearest, worldImpact, worldDir, rng, 0f);
			changed = true;
		}

		changed |= SolveSupport(worldImpact, worldDir, rng);
		if (changed) _mesh.Mesh = BuildStandingMesh();
		FlushDust();
	}

	public void ResetPane()
	{
		foreach (var body in _shards)
			if (GodotObject.IsInstanceValid(body)) body.QueueFree();
		_shards.Clear();
		_liveShardEstimate = 0;
		_pendingDust.Clear();

		if (_cells != null)
		{
			foreach (var cell in _cells)
				if (cell.Col != null && GodotObject.IsInstanceValid(cell.Col)) cell.Col.QueueFree();
			_cells = null;
		}
		_adjacency = null;
		_frameSupport = null;
		_impacts.Clear();
		UpdateCracks();

		if (_pile != null) { _pile.Mesh = null; _pileMesh = null; }
		if (_dust != null) _dust.Emitting = false;

		_mesh.Mesh = BuildIntactMesh();
		_mesh.Visible = true;
		if (_boxCollision != null) _boxCollision.Disabled = false;
		_state = PaneState.Intact;
	}
}
