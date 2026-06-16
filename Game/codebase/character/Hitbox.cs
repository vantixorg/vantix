using Godot;

namespace Vantix.Character;

/// <summary>
/// Scene-node hitbox dropped under a BoneAttachment3D. HitboxRig scans the skeleton in _Ready and
/// sets the layer / self-exclude RIDs. Group routes damage via WeaponStats.Damages.
/// </summary>
[Tool, GlobalClass]
public partial class Hitbox : StaticBody3D
{
	/// <summary>Zone (Head/Chest/Arm/...); keys the WeaponStats.Damages lookup.</summary>
	[Export] public HitboxGroup Group = HitboxGroup.Body;

	/// <summary>Sets collision layer/mask and joins the "flesh" group.</summary>
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		CollisionLayer = HitboxRig.Layer;
		CollisionMask = 0u;
		if (!IsInGroup("flesh")) AddToGroup("flesh");
	}
}
