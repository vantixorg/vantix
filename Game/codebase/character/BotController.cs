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

namespace Vantix.Character;

/// <summary>
/// Drives a bot's input so it walks the map via NavMesh path to a random target (picks a new one
/// on arrival). Short-range steering is reactive: probes the desired heading plus offsets each tick
/// and takes the first clear lane, handling dynamic obstacles the NavMesh ignores. Stuck-detection
/// re-requests a path. Bots walk only.
///
/// Pure logic, ticked by NetServer.Poll before FeedInputsToAgents. Raycast query/result are pooled.
/// </summary>
public class BotController
{
	/// <summary>Distance at which the target counts as reached.</summary>
	private const float ArriveRadius = 2.5f;
	/// <summary>Hard timeout per target (~5s at 128 Hz); after this the path is assumed unreachable.</summary>
	private const uint MaxTicksPerTarget = 640;
	/// <summary>Stuck-check window: moved less than StuckMinMovedMeters in this many ticks = re-pick target.</summary>
	private const uint StuckCheckTicks = 128;
	private const float StuckMinMovedMeters = 0.5f;
	/// <summary>Max body-yaw turn per tick (radians). 0.05 ≈ 366°/sec.</summary>
	private const float MaxYawTurnPerTick = 0.05f;
	/// <summary>Forward probe distance (~1s reaction at walk speed).</summary>
	private const float ProbeDistance = 3.5f;
	/// <summary>Chest-level probe height (walls).</summary>
	private const float ProbeHeight = 1.0f;
	/// <summary>Knee-level probe height (crates, fences).</summary>
	private const float LowProbeHeight = 0.4f;
	/// <summary>Half-width of the body capsule footprint, for edge probes.</summary>
	private const float ProbeHalfWidth = 0.4f;
	/// <summary>Yaw offsets (radians) tried in order when blocked. Capped at ±1.4 rad (~80°);
	/// wider angles caused spinning in tight corners.</summary>
	private static readonly float[] ProbeOffsets =
	{
		0.0f,
		-0.35f, 0.35f,
		-0.7f,  0.7f,
		-1.05f, 1.05f,
		-1.4f,  1.4f,
	};

	private Vector3 _target;
	private float _smoothYaw;
	private float _smoothPitch;
	private uint _targetSetTick;
	private Vector3 _stuckCheckPos;
	private uint _stuckCheckTick;
	private bool _initialized;

	private Vector3[] _navPath;
	private int _navPathIndex;

	private float _lastChosenOffset;

	private byte _targetEnemyNetId;
	private uint _targetAcquiredTick;
	private bool _aimAtHead;

	private PhysicsRayQueryParameters3D _query;
	private readonly PhysicsRayQueryResult3D _result = new();

	private readonly RandomNumberGenerator _rng = new();

	/// <summary>Standing eye height; must match NetworkPlayer.StandEyeHeight so LOS matches hitscan.</summary>
	private const float EyeHeight = 1.7f;
	/// <summary>Beyond this, bots ignore targets.</summary>
	private const float MaxEngagementDistance = 50f;

	/// <summary>Ties this controller to a body. Re-calling resets state (respawn).</summary>
	public void Init(Vector3 startPos, float startYaw, uint currentTick)
	{
		_rng.Randomize();
		_smoothYaw = startYaw;
		_stuckCheckPos = startPos;
		_stuckCheckTick = currentTick;
		_navPath = null;
		_navPathIndex = 0;
		_target = startPos;
		_initialized = true;
	}

	/// <summary>Builds this tick's input. If a visible enemy is in range, turn toward it and fire once
	/// aim is aligned and the reaction delay elapsed; otherwise follow the NavMesh path, re-requesting
	/// on arrival or when stuck.</summary>
	public InputPacket Tick(uint tickIndex, Vector3 currentPos, Rid navMap,
		IReadOnlyList<Vector3> targetCandidates,
		PhysicsDirectSpaceState3D space, uint probeMask, BotCombatContext combat)
	{
		if (!_initialized)
			Init(currentPos, _smoothYaw, tickIndex);

		PeerState enemy = DetectBestEnemy(currentPos, space, combat);
		if (enemy != null)
		{
			if (_targetEnemyNetId != enemy.NetId)
			{
				_targetEnemyNetId = enemy.NetId;
				_targetAcquiredTick = tickIndex;
				_aimAtHead = Mathf.Clamp(combat.Difficulty, 0, 3) switch
				{
					>= 3 => true,
					2 => _rng.Randf() > 0.5f,
					_ => false,
				};
				_stuckCheckPos = currentPos;
				_stuckCheckTick = tickIndex;
			}

			Vector3 enemyAimPoint = ComputeAimPoint(enemy.ServerAgent.GlobalPosition, combat.Difficulty);
			Vector3 eye = currentPos + Vector3.Up * EyeHeight;
			Vector3 toEnemy = enemyAimPoint - eye;
			float horiz = Mathf.Sqrt(toEnemy.X * toEnemy.X + toEnemy.Z * toEnemy.Z);
			float desiredYaw = Mathf.Atan2(-toEnemy.X, -toEnemy.Z);
			float desiredPitch = horiz > 0.01f ? Mathf.Atan2(toEnemy.Y, horiz) : 0f;

			float combatYawRate = GetCombatYawTurnPerTick(combat.Difficulty, combat.TickRate);
			const float AimDeadband = 0.003f;
			float rawYawDelta = Mathf.AngleDifference(_smoothYaw, desiredYaw);
			if (Mathf.Abs(rawYawDelta) > AimDeadband)
				_smoothYaw += Mathf.Clamp(rawYawDelta, -combatYawRate, combatYawRate);
			float rawPitchDelta = desiredPitch - _smoothPitch;
			if (Mathf.Abs(rawPitchDelta) > AimDeadband)
				_smoothPitch = Mathf.Clamp(_smoothPitch + Mathf.Clamp(rawPitchDelta, -combatYawRate, combatYawRate), -1.4f, 1.4f);

			float alignThreshold = combat.Difficulty switch { >= 3 => 0.05f, 2 => 0.10f, 1 => 0.18f, _ => 0.3f };
			bool aimAligned = Mathf.Abs(Mathf.AngleDifference(_smoothYaw, desiredYaw)) < alignThreshold;
			uint reactionTicks = GetReactionTicks(combat.Difficulty, combat.TickRate);
			bool reactionElapsed = tickIndex - _targetAcquiredTick >= reactionTicks;
			bool canFire = aimAligned && reactionElapsed;

			return new InputPacket
			{
				TickIndex = tickIndex,
				ViewYaw = _smoothYaw,
				ViewPitch = _smoothPitch,
				InitialViewYaw = _smoothYaw,
				InitialViewPitch = _smoothPitch,
				WishX = 0f,
				WishZ = 0f,
				SprintHeld = false,
				FirePressed = canFire && !combat.NeedsReload,
				ReloadPressed = combat.NeedsReload,
				Events = null,
				InitialBits = 0,
			};
		}

		_targetEnemyNetId = 0;
		_smoothPitch = Mathf.MoveToward(_smoothPitch, 0f, 0.05f);

		if (targetCandidates == null || targetCandidates.Count == 0 || !navMap.IsValid)
		{
			return new InputPacket { TickIndex = tickIndex, ViewYaw = _smoothYaw, InitialViewYaw = _smoothYaw };
		}

		if (_navPath == null || _navPathIndex >= _navPath.Length)
			RequestNavPath(currentPos, navMap, targetCandidates, tickIndex);

		if (tickIndex - _stuckCheckTick >= StuckCheckTicks)
		{
			if ((currentPos - _stuckCheckPos).LengthSquared() < StuckMinMovedMeters * StuckMinMovedMeters)
				RequestNavPath(currentPos, navMap, targetCandidates, tickIndex);
			_stuckCheckPos = currentPos;
			_stuckCheckTick = tickIndex;
		}

		Vector3 delta = _target - currentPos;
		delta.Y = 0f;
		float distance = delta.Length();

		if (distance < ArriveRadius || tickIndex - _targetSetTick > MaxTicksPerTarget)
		{
			_navPathIndex++;
			if (_navPath == null || _navPathIndex >= _navPath.Length)
				RequestNavPath(currentPos, navMap, targetCandidates, tickIndex);
			else
			{
				_target = _navPath[_navPathIndex];
				_targetSetTick = tickIndex;
			}
			delta = _target - currentPos;
			delta.Y = 0f;
			distance = delta.Length();
		}

		float wanderYaw = distance > 0.01f ? Mathf.Atan2(-delta.X, -delta.Z) : _smoothYaw;
		if (space != null)
			wanderYaw = FindClearYaw(space, currentPos, wanderYaw, probeMask);

		float wanderYawDelta = Mathf.AngleDifference(_smoothYaw, wanderYaw);
		wanderYawDelta = Mathf.Clamp(wanderYawDelta, -MaxYawTurnPerTick, MaxYawTurnPerTick);
		_smoothYaw += wanderYawDelta;

		return new InputPacket
		{
			TickIndex = tickIndex,
			ViewYaw = _smoothYaw,
			ViewPitch = _smoothPitch,
			InitialViewYaw = _smoothYaw,
			InitialViewPitch = _smoothPitch,
			WishX = 0f,
			WishZ = -1f,
			SprintHeld = false,
			ShiftHeld = false,
			ReloadPressed = combat.NeedsReload,
			Events = null,
			InitialBits = 0,
		};
	}

	/// <summary>Reaction delay before first shot, in ticks; applied on every (re-)acquire. Derived from a
	/// per-difficulty ms target via tick rate so it's identical at 64 vs 128 Hz.</summary>
	private static uint GetReactionTicks(int difficulty, int tickRate)
	{
		int ms = Mathf.Clamp(difficulty, 0, 3) switch { >= 3 => 220, 2 => 320, 1 => 450, _ => 600 };
		return (uint)Mathf.Max(1, (ms * tickRate) / 1000);
	}

	/// <summary>Max yaw turn per tick in combat; higher difficulty = faster snap-to-aim.
	/// Rates: diff0 ≈ 170°/s, diff1 ≈ 290°/s, diff2 ≈ 460°/s, diff3 ≈ 800°/s.</summary>
	private static float GetCombatYawTurnPerTick(int difficulty, int tickRate)
	{
		float radPerSec = Mathf.Clamp(difficulty, 0, 3) switch { >= 3 => 14f, 2 => 8f, 1 => 5f, _ => 3f };
		return radPerSec / Mathf.Max(1, tickRate);
	}

	/// <summary>Aim point above enemy feet; lower difficulty aims lower, higher aims at the head.
	/// Diff 2 alternates body/head per acquire via _aimAtHead.</summary>
	private Vector3 ComputeAimPoint(Vector3 enemyFeetPos, int difficulty)
	{
		float aimY = Mathf.Clamp(difficulty, 0, 3) switch
		{
			>= 3 => 1.65f,
			2 => _aimAtHead ? 1.65f : 1.0f,
			1 => 1.0f,
			_ => 0.4f,
		};
		return enemyFeetPos + Vector3.Up * aimY;
	}

	/// <summary>Closest hostile, alive, in-range peer with clear LOS, or null.
	/// Skips self, same-team, dead and unspawned peers.</summary>
	private PeerState DetectBestEnemy(Vector3 currentPos, PhysicsDirectSpaceState3D space, BotCombatContext combat)
	{
		if (space == null || combat.AllPeers == null)
			return null;
		Vector3 eye = currentPos + Vector3.Up * EyeHeight;
		PeerState best = null;
		float bestDistSq = MaxEngagementDistance * MaxEngagementDistance;
		foreach (var peer in combat.AllPeers)
		{
			if (peer.NetId == combat.OwnNetId)
				continue;
			if (peer.IsBot)
				continue;
			if (peer.Team == combat.OwnTeam)
				continue;
			if (peer.Hp == 0)
				continue;
			var agent = peer.ServerAgent;
			if (agent == null || agent.IsDead || agent.IsFrozen)
				continue;

			Vector3 enemyFeet = agent.GlobalPosition;
			float distSq = (enemyFeet - currentPos).LengthSquared();
			if (distSq > bestDistSq)
				continue;

			if (!HasFullLineOfSight(space, eye, enemyFeet))
				continue;

			best = peer;
			bestDistSq = distSq;
		}
		return best;
	}

	/// <summary>True if eye-to-head/chest/waist rays are all clear of world geometry.
	/// Mask=1 (world only) excludes agent bodies, which would otherwise self-block the target.</summary>
	private bool HasFullLineOfSight(PhysicsDirectSpaceState3D space, Vector3 eye, Vector3 enemyFeetPos)
	{
		if (_query == null)
		{
			_query = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Right);
			_query.CollideWithAreas = false;
			_query.CollideWithBodies = true;
		}
		_query.CollisionMask = 1u;
		_query.From = eye;

		_query.To = enemyFeetPos + Vector3.Up * 1.65f;
		if (space.IntersectRayInto(_query, _result))
			return false;
		_query.To = enemyFeetPos + Vector3.Up * 1.0f;
		if (space.IntersectRayInto(_query, _result))
			return false;
		_query.To = enemyFeetPos + Vector3.Up * 0.5f;
		if (space.IntersectRayInto(_query, _result))
			return false;
		return true;
	}

	/// <summary>First yaw (desired heading or a wider offset) with a clear capsule lane. Keeps last
	/// tick's offset while still clear (hysteresis) to avoid oscillation. Returns desiredYaw if all
	/// offsets blocked; stuck-detection then re-requests a path.</summary>
	private float FindClearYaw(PhysicsDirectSpaceState3D space, Vector3 currentPos, float desiredYaw, uint probeMask)
	{
		if (_lastChosenOffset != 0f && IsLaneClear(space, currentPos, desiredYaw + _lastChosenOffset, probeMask))
			return desiredYaw + _lastChosenOffset;
		foreach (float offset in ProbeOffsets)
		{
			float candidateYaw = desiredYaw + offset;
			if (IsLaneClear(space, currentPos, candidateYaw, probeMask))
			{
				_lastChosenOffset = offset;
				return candidateYaw;
			}
		}
		_lastChosenOffset = 0f;
		return desiredYaw;
	}

	/// <summary>True if all six probes (left/centre/right × chest/knee) along yaw are clear.
	/// Forward = (-sin yaw, 0, -cos yaw), matching MovementInput.AimDirection.</summary>
	private bool IsLaneClear(PhysicsDirectSpaceState3D space, Vector3 currentPos, float yaw, uint probeMask)
	{
		if (_query == null)
		{
			_query = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Right);
			_query.CollideWithAreas = false;
			_query.CollideWithBodies = true;
		}
		_query.CollisionMask = probeMask;

		Vector3 forward = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
		Vector3 right = new Vector3(-Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
		Vector3 chestBase = currentPos + Vector3.Up * ProbeHeight;
		Vector3 kneeBase = currentPos + Vector3.Up * LowProbeHeight;
		Vector3 sideOffset = right * ProbeHalfWidth;
		Vector3 forwardOffset = forward * ProbeDistance;

		return ProbeRay(space, chestBase - sideOffset, forwardOffset)
			&& ProbeRay(space, chestBase, forwardOffset)
			&& ProbeRay(space, chestBase + sideOffset, forwardOffset)
			&& ProbeRay(space, kneeBase - sideOffset, forwardOffset)
			&& ProbeRay(space, kneeBase, forwardOffset)
			&& ProbeRay(space, kneeBase + sideOffset, forwardOffset);
	}

	/// <summary>Single-ray helper; true if the segment is clear.</summary>
	private bool ProbeRay(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 delta)
	{
		_query.From = from;
		_query.To = from + delta;
		return !space.IntersectRayInto(_query, _result);
	}

	/// <summary>Picks a fresh target and queries the NavMesh for the waypoint sequence to reach it.
	/// Falls back to a single-waypoint path (the candidate) when the NavMesh returns nothing.</summary>
	private const float MinTargetDistanceSquared = 25f;
	private const int MaxPickAttempts = 8;
	private void RequestNavPath(Vector3 currentPos, Rid navMap, IReadOnlyList<Vector3> targetCandidates, uint tickIndex)
	{
		_targetSetTick = tickIndex;
		_navPathIndex = 0;

		Vector3 candidate = Vector3.Zero;
		bool haveCandidate = false;
		for (int attempt = 0; attempt < MaxPickAttempts; attempt++)
		{
			int idx = _rng.RandiRange(0, targetCandidates.Count - 1);
			Vector3 c = targetCandidates[idx];
			if ((c - currentPos).LengthSquared() < MinTargetDistanceSquared)
				continue;
			candidate = c;
			haveCandidate = true;
			break;
		}
		if (!haveCandidate)
			candidate = targetCandidates[_rng.RandiRange(0, targetCandidates.Count - 1)];

		_navPath = NavigationServer3D.MapGetPath(navMap, currentPos, candidate, optimize: true);
		if (_navPath == null || _navPath.Length == 0)
		{
			_navPath = new[] { candidate };
			_navPathIndex = 0;
		}
		if (_navPath.Length > 1 && (_navPath[0] - currentPos).LengthSquared() < ArriveRadius * ArriveRadius)
			_navPathIndex = 1;
		_target = _navPath[_navPathIndex];
	}
}
