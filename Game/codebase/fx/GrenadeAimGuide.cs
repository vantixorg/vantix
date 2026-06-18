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

/// <summary>
/// Draws the predicted grenade trajectory as a camera-facing ImmediateMesh ribbon plus a landing ring,
/// from GrenadeTrajectory.Predict path points. TopLevel = true so points are in world coordinates.
/// </summary>
public partial class GrenadeAimGuide : Node3D
{
	private const float HalfWidth = 0.025f;
	private const float MinSpacing = 0.07f;
	private const float RingRadius = 0.5f;

	private static readonly Color GuideColor = new(0.95f, 0.13f, 0.1f, 0.65f);

	private MeshInstance3D _line;
	private MeshInstance3D _ring;
	private ImmediateMesh _mesh;
	private readonly List<Vector3> _pts = new();
	private bool _built;

	/// <summary>Ensures meshes exist on tree entry.</summary>
	public override void _Ready() => EnsureBuilt();

	/// <summary>Builds line and ring lazily, whichever of _Ready/UpdatePath/SetGuideVisible runs first.</summary>
	private void EnsureBuilt()
	{
		if (_built) return;
		_built = true;
		TopLevel = true;

		var lineMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			VertexColorUseAsAlbedo = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			EmissionEnabled = true,
			Emission = new Color(GuideColor.R, GuideColor.G, GuideColor.B),
			EmissionEnergyMultiplier = 0.5f,
		};
		_mesh = new ImmediateMesh();
		_line = new MeshInstance3D
		{
			Mesh = _mesh,
			MaterialOverride = lineMat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			CustomAabb = new Aabb(new Vector3(-4096f, -4096f, -4096f), new Vector3(8192f, 8192f, 8192f)),
		};
		AddChild(_line);

		var ringMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			AlbedoColor = GuideColor,
			EmissionEnabled = true,
			Emission = new Color(GuideColor.R, GuideColor.G, GuideColor.B),
			EmissionEnergyMultiplier = 0.5f,
		};
		_ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = RingRadius - 0.022f, OuterRadius = RingRadius + 0.022f },
			MaterialOverride = ringMat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(_ring);

		Visible = false;
	}

	/// <summary>Shows or hides the entire aim guide.</summary>
	public void SetGuideVisible(bool visible)
	{
		EnsureBuilt();
		Visible = visible;
	}

	/// <summary>Rebuilds line and ring from a predicted trajectory; points are world coordinates.</summary>
	public void UpdatePath(IReadOnlyList<Vector3> points, Vector3 landing, Vector3 landingNormal)
	{
		EnsureBuilt();
		_pts.Clear();
		if (points.Count > 0)
		{
			_pts.Add(points[0]);
			for (int i = 1; i < points.Count - 1; i++)
				if (points[i].DistanceSquaredTo(_pts[_pts.Count - 1]) >= MinSpacing * MinSpacing)
					_pts.Add(points[i]);
			if (points.Count > 1) _pts.Add(points[points.Count - 1]);
		}

		_mesh.ClearSurfaces();
		if (_pts.Count >= 2)
		{
			Camera3D cam = GetViewport()?.GetCamera3D();
			Vector3 camPos = cam?.GlobalPosition ?? _pts[0];
			int segs = _pts.Count - 1;

			_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
			for (int i = 0; i < segs; i++)
			{
				Vector3 a = _pts[i], b = _pts[i + 1];
				Vector3 sideA = SideVec(a, i > 0 ? _pts[i - 1] : a, b, camPos);
				Vector3 sideB = SideVec(b, a, i + 2 < _pts.Count ? _pts[i + 2] : b, camPos);
				Color cA = FadeColor((float)i / segs);
				Color cB = FadeColor((float)(i + 1) / segs);

				Vert(a - sideA, cA); Vert(a + sideA, cA); Vert(b + sideB, cB);
				Vert(a - sideA, cA); Vert(b + sideB, cB); Vert(b - sideB, cB);
			}
			_mesh.SurfaceEnd();
		}

		_ring.Visible = _pts.Count >= 2;
		if (_ring.Visible)
		{
			Vector3 up = landingNormal.LengthSquared() > 0.01f ? landingNormal.Normalized() : Vector3.Up;
			_ring.GlobalTransform = AlignToNormal(landing + up * 0.03f, up);
		}
	}

	/// <summary>Emits one colored vertex into the current surface.</summary>
	private void Vert(Vector3 v, Color c)
	{
		_mesh.SurfaceSetColor(c);
		_mesh.SurfaceAddVertex(v);
	}

	/// <summary>Brightens/opaques the color toward the landing so the line points at the target.</summary>
	private static Color FadeColor(float t)
	{
		float b = Mathf.Lerp(0.5f, 1f, t);
		return new Color(GuideColor.R * b, GuideColor.G * b, GuideColor.B * b,
			GuideColor.A * Mathf.Lerp(0.6f, 1f, t));
	}

	/// <summary>Ribbon side vector at p: perpendicular to both path tangent and view direction.</summary>
	private static Vector3 SideVec(Vector3 p, Vector3 prev, Vector3 next, Vector3 camPos)
	{
		Vector3 tan = next - prev;
		if (tan.LengthSquared() < 1e-8f) tan = Vector3.Right;
		tan = tan.Normalized();
		Vector3 side = tan.Cross(camPos - p);
		if (side.LengthSquared() < 1e-8f) side = tan.Cross(Vector3.Up);
		if (side.LengthSquared() < 1e-8f) side = Vector3.Right;
		return side.Normalized() * HalfWidth;
	}

	/// <summary>Transform with the Y axis along up, so the ring lies flat on the surface.</summary>
	private static Transform3D AlignToNormal(Vector3 pos, Vector3 up)
	{
		Vector3 fwd = Mathf.Abs(up.Dot(Vector3.Forward)) > 0.9f ? Vector3.Right : Vector3.Forward;
		Vector3 right = up.Cross(fwd).Normalized();
		fwd = right.Cross(up).Normalized();
		return new Transform3D(new Basis(right, up, fwd), pos);
	}
}
