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

namespace Vantix.Editor;

#if TOOLS
using Godot;

/// <summary>Draws a wireframe box for every Spawn node. Registered by SpotsGizmoPlugin. Green, so spawn
/// regions read distinctly from BombSpot (red) and Zone (cyan).</summary>
[Tool]
public partial class SpawnGizmoPlugin : EditorNode3DGizmoPlugin
{
	private const string OutlineMat = "spawn_outline";
	private const string FillMat = "spawn_fill";

	public SpawnGizmoPlugin()
	{
		CreateMaterial(OutlineMat, new Color(0.30f, 0.95f, 0.40f));
		var fill = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.30f, 0.95f, 0.40f, 0.12f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		AddMaterial(FillMat, fill);
	}

	public override string _GetGizmoName() => "Spawn";
	public override bool _HasGizmo(Node3D node) => node is Spawn;

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		gizmo.Clear();
		if (gizmo.GetNode3D() is not Spawn s) return;
		gizmo.AddLines(GizmoBoxBuilder.BuildLines(s.Size), GetMaterial(OutlineMat, gizmo));
		gizmo.AddMesh(GizmoBoxBuilder.BuildBoxMesh(s.Size), GetMaterial(FillMat, gizmo));
	}
}
#endif
