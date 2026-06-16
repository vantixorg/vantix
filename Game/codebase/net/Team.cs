namespace Vantix.Server;

/// <summary>Which spawn pool a player uses. Byte values are wire-format — do not renumber.
/// Display names live in <see cref="Teams"/>.</summary>
public enum Team : byte
{
	/// <summary>"VEKTOR". Marker group "spawn_team1".</summary>
	Team1 = 0,
	/// <summary>"ATLAS-9". Marker group "spawn_team2".</summary>
	Team2 = 1,
	/// <summary>Deathmatch / FFA. Marker group "spawn_deathmatch".</summary>
	Deathmatch = 2,
	/// <summary>Competitive pre-pick state: no spawn pose, no LocalPlayer, client cycles preview cameras.
	/// TeamSelect switches to Team1/Team2, then the server replies SpawnAuthorize with the real pose.</summary>
	Spectator = 3,
}
