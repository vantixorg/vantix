using System.Collections.Generic;
using Godot;

namespace Vantix.Character;

/// <summary>Per-tick world snapshot a bot reads to choose its movement and fire decisions.</summary>
public struct BotCombatContext
{
	public List<PeerState> AllPeers;
	public byte OwnNetId;
	public Team OwnTeam;
	public int Difficulty;
	public int TickRate;

	/// <summary>Magazine empty and not already reloading. Drives ReloadPressed; the edge fires the reload once.</summary>
	public bool NeedsReload;
}
