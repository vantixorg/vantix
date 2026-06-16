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
