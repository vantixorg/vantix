using Godot;

namespace Vantix.Levels;

/// <summary>
/// A respawn region (a Zone); players land at the area centre, or a sampled cell when several spawn
/// together. The Kind tag selects the mode/team pool, resolved by SpawnManager.
/// </summary>
[Tool, GlobalClass]
public partial class Spawn : Zone
{
	/// <summary>Spawn pool (deathmatch / team 1 / team 2) this region belongs to.</summary>
	public enum SpawnKind { Deathmatch, Team1, Team2 }

	/// <summary>Spawn pool (Deathmatch/Team1/Team2) this region belongs to.</summary>
	[Export] public SpawnKind Kind { get; set; } = SpawnKind.Deathmatch;
}
