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

/// <summary>
/// Editor plugin entry point: registers the wireframe-box gizmos for Zone, BombSpot, and Spawn. The
/// nodes themselves render no editor-visible geometry.
/// </summary>
[Tool]
public partial class SpotsGizmoPlugin : EditorPlugin
{
	private ZoneGizmoPlugin _zoneGizmo;
	private BombSpotGizmoPlugin _bombSpotGizmo;
	private SpawnGizmoPlugin _spawnGizmo;

	public override void _EnterTree()
	{
		_zoneGizmo = new ZoneGizmoPlugin();
		_bombSpotGizmo = new BombSpotGizmoPlugin();
		_spawnGizmo = new SpawnGizmoPlugin();
		AddNode3DGizmoPlugin(_zoneGizmo);
		AddNode3DGizmoPlugin(_bombSpotGizmo);
		AddNode3DGizmoPlugin(_spawnGizmo);
	}

	public override void _ExitTree()
	{
		if (_zoneGizmo != null) RemoveNode3DGizmoPlugin(_zoneGizmo);
		if (_bombSpotGizmo != null) RemoveNode3DGizmoPlugin(_bombSpotGizmo);
		if (_spawnGizmo != null) RemoveNode3DGizmoPlugin(_spawnGizmo);
		_zoneGizmo = null;
		_bombSpotGizmo = null;
		_spawnGizmo = null;
	}
}
#endif
