namespace Vantix.Server;

/// <summary>Active game mode; determines the spawn pool. Future round-manager picks it per match.</summary>
public enum GameMode : byte
{
	/// <summary>Round-based CT vs T. Joiners are assigned alternately.</summary>
	Competitive = 0,
	/// <summary>Free-for-all — everyone uses the Deathmatch pool.</summary>
	Deathmatch = 1,
}
