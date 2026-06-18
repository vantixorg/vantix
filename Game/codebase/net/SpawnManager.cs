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

using Godot;
using System.Collections.Generic;

namespace Vantix.Server;

/// <summary>Display names + tints per team. Single source of truth for UI (Scoreboard, TeamSelectionMenu, KillFeed).</summary>
public static class Teams
{
	public const string Team1Name = "VEKTOR";
	public const string Team2Name = "ATLAS-9";
	public const string DeathmatchName = "DEATHMATCH";
	public const string SpectatorName = "SPECTATOR";

	public static readonly Color Team1Color = new(0.30f, 0.60f, 1.00f);
	public static readonly Color Team2Color = new(1.00f, 0.65f, 0.20f);

	public static string DisplayName(Team t) => t switch
	{
		Team.Team1 => Team1Name,
		Team.Team2 => Team2Name,
		Team.Deathmatch => DeathmatchName,
		Team.Spectator => SpectatorName,
		_ => t.ToString(),
	};

	public static Color DisplayColor(Team t) => t switch
	{
		Team.Team1 => Team1Color,
		Team.Team2 => Team2Color,
		_ => Colors.White,
	};
}

/// <summary>Spawn-marker management for round modes (CT vs T) and Deathmatch. On map load, buckets the
/// active <see cref="Level"/>'s Spawn nodes by Kind. Mapper convention: add Spawn nodes (set Kind + Size),
/// list them in <see cref="Level.SpawnPaths"/>, ~4-10 per team. Falls back to a hard-coded position with no spawns.</summary>
public class SpawnManager
{
	/// <summary>One spawn marker resolved to position and yaw.</summary>
	private struct SpawnPoint { public Vector3 Pos; public float Yaw; }

	private readonly List<SpawnPoint> _ctSpawns = new();
	private readonly List<SpawnPoint> _tSpawns = new();
	private readonly List<SpawnPoint> _dmSpawns = new();
	private int _ctRotator;
	private int _tRotator;
	private int _dmRotator;

	public bool Initialized { get; private set; }
	public int CtCount => _ctSpawns.Count;
	public int TCount => _tSpawns.Count;
	public int DmCount => _dmSpawns.Count;

	/// <summary>Fallback position when the map has no markers.</summary>
	public static readonly Vector3 DefaultPos = new(9.857169f, 1.0f, 2.1423106f);

	/// <summary>Min distance (m) from an occupied spawn for a slot to count as free.</summary>
	public const float FreeRadius = 1.0f;

	/// <summary>Reads spawns from the active <see cref="World.Level"/>; idempotent, safe to re-call on reload.
	/// <paramref name="tree"/> is kept for call-site compatibility but no longer walked.</summary>
	public void Scan(SceneTree tree)
	{
		_ctSpawns.Clear();
		_tSpawns.Clear();
		_dmSpawns.Clear();
		_ctRotator = 0;
		_tRotator = 0;
		_dmRotator = 0;

		// Spawn extends Zone (Area3D); centre + yaw become the pose. Players sharing an area
		// get de-clumped by the FreeRadius retry in PickFromList.
		var level = World.Level;
		if (level != null)
		{
			foreach (var sp in level.SpawnsForKind(Spawn.SpawnKind.Team1)) _ctSpawns.Add(AreaToPoint(sp));
			foreach (var sp in level.SpawnsForKind(Spawn.SpawnKind.Team2)) _tSpawns.Add(AreaToPoint(sp));
			foreach (var sp in level.SpawnsForKind(Spawn.SpawnKind.Deathmatch)) _dmSpawns.Add(AreaToPoint(sp));
		}

		Initialized = true;
		Dbg.Print($"[SpawnManager] Scan: {_ctSpawns.Count} CT, {_tSpawns.Count} T, {_dmSpawns.Count} DM spawns found");
		if (_ctSpawns.Count == 0 && _tSpawns.Count == 0 && _dmSpawns.Count == 0)
			GD.PushWarning("[SpawnManager] No spawns in Level — falling back to DefaultPos. Add Spawn nodes to the map and list them in Level.SpawnPaths.");
	}

	/// <summary>Spawn (Area3D) → SpawnPoint from the area's centre and yaw.</summary>
	private static SpawnPoint AreaToPoint(Spawn s) =>
		new() { Pos = s.GlobalPosition, Yaw = s.GlobalRotation.Y };

	/// <summary>Picks a free slot for the team, falling back to the other team, Deathmatch, then DefaultPos.</summary>
	public (Vector3 pos, float yaw) PickFreeSpawn(Team team, IReadOnlyList<Vector3> occupied)
	{
		switch (team)
		{
			case Team.Team1:
				if (_ctSpawns.Count > 0) return PickFromList(_ctSpawns, ref _ctRotator, occupied);
				break;
			case Team.Team2:
				if (_tSpawns.Count > 0) return PickFromList(_tSpawns, ref _tRotator, occupied);
				break;
			case Team.Deathmatch:
				if (_dmSpawns.Count > 0) return PickFromList(_dmSpawns, ref _dmRotator, occupied);
				break;
		}
		if (_dmSpawns.Count > 0) return PickFromList(_dmSpawns, ref _dmRotator, occupied);
		if (_ctSpawns.Count > 0) return PickFromList(_ctSpawns, ref _ctRotator, occupied);
		if (_tSpawns.Count > 0)  return PickFromList(_tSpawns,  ref _tRotator,  occupied);
		return (DefaultPos, 0f);
	}

	/// <summary>Rotates through the list, returning the first slot passing FreeRadius; if all occupied, returns the next rotating slot.</summary>
	private static (Vector3 pos, float yaw) PickFromList(List<SpawnPoint> list, ref int rotator, IReadOnlyList<Vector3> occupied)
	{
		for (int attempt = 0; attempt < list.Count; attempt++)
		{
			int idx = (rotator + attempt) % list.Count;
			var pt = list[idx];
			if (IsFree(pt.Pos, occupied))
			{
				rotator = (idx + 1) % list.Count;
				return (pt.Pos, pt.Yaw);
			}
		}
		var fb = list[rotator % list.Count];
		rotator = (rotator + 1) % list.Count;
		return (fb.Pos, fb.Yaw);
	}

	/// <summary>True when no occupied position lies within FreeRadius of the slot.</summary>
	private static bool IsFree(Vector3 pos, IReadOnlyList<Vector3> occupied)
	{
		float r2 = FreeRadius * FreeRadius;
		foreach (var o in occupied)
			if (pos.DistanceSquaredTo(o) < r2) return false;
		return true;
	}
}
