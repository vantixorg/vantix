using Godot;

namespace Vantix.Levels;

/// <summary>
/// A named bomb plant region (A/B/C); a Zone plus a Slot tag. Used for HUD compass markers and as bot
/// nav targets. Resolved through the Level registry by slot.
/// </summary>
[Tool, GlobalClass]
public partial class BombSpot : Zone
{
	/// <summary>Bomb plant site this spot represents (A/B/C).</summary>
	public enum BombSlot { A, B, C }

	/// <summary>Plant slot (A/B/C) this spot represents.</summary>
	[Export] public BombSlot Slot { get; set; } = BombSlot.A;
}
