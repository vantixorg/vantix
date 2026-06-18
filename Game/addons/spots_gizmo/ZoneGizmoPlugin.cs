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

/// <summary>Draws a wireframe box for every Zone in the edited scene. Registered by SpotsGizmoPlugin;
/// visibility follows the 3D View → Gizmos toggle (the reason for a gizmo plugin over a child
/// MeshInstance3D). The Zone.Size setter triggers the redraw.</summary>
[Tool]
public partial class ZoneGizmoPlugin : EditorNode3DGizmoPlugin
{
	private const string OutlineMat = "zone_outline";
	private const string FillMat = "zone_fill";

	public ZoneGizmoPlugin()
	{
		// Cyan — distinguishes Zone outlines from BombSpot's red.
		CreateMaterial(OutlineMat, new Color(0.30f, 0.85f, 1.00f));
		// Unshaded + alpha + no-cull so the fill reads from any angle.
		var fill = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.30f, 0.85f, 1.00f, 0.12f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		AddMaterial(FillMat, fill);
	}

	public override string _GetGizmoName() => "Zone";
	// Plain Zone only. BombSpot and Spawn extend Zone and have their own plugins; without the
	// exclusion they'd draw twice.
	public override bool _HasGizmo(Node3D node) => node is Zone && node is not BombSpot && node is not Spawn;

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		gizmo.Clear();
		if (gizmo.GetNode3D() is not Zone z) return;
		gizmo.AddLines(GizmoBoxBuilder.BuildLines(z.Size), GetMaterial(OutlineMat, gizmo));
		gizmo.AddMesh(GizmoBoxBuilder.BuildBoxMesh(z.Size), GetMaterial(FillMat, gizmo));
	}
}
#endif
