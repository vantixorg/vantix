namespace Vantix.Editor;

#if TOOLS
using Godot;

/// <summary>Draws a wireframe box for every BombSpot in the edited scene. Registered by
/// SpotsGizmoPlugin; visibility follows the 3D View → Gizmos toggle. The BombSpot.Size setter triggers
/// the redraw.</summary>
[Tool]
public partial class BombSpotGizmoPlugin : EditorNode3DGizmoPlugin
{
	private const string OutlineMat = "bombspot_outline";
	private const string FillMat = "bombspot_fill";

	public BombSpotGizmoPlugin()
	{
		// Red — distinguishes plant regions from Zones (cyan).
		CreateMaterial(OutlineMat, new Color(1.00f, 0.30f, 0.25f));
		// Unshaded + alpha + no-cull so the fill reads from any angle.
		var fill = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.00f, 0.30f, 0.25f, 0.14f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		AddMaterial(FillMat, fill);
	}

	public override string _GetGizmoName() => "BombSpot";
	public override bool _HasGizmo(Node3D node) => node is BombSpot;

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		gizmo.Clear();
		if (gizmo.GetNode3D() is not BombSpot bs) return;
		gizmo.AddLines(GizmoBoxBuilder.BuildLines(bs.Size), GetMaterial(OutlineMat, gizmo));
		gizmo.AddMesh(GizmoBoxBuilder.BuildBoxMesh(bs.Size), GetMaterial(FillMat, gizmo));
	}
}
#endif
