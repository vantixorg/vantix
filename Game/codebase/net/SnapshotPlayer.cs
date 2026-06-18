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

namespace Vantix.Net;

/// <summary>One player's state within a server snapshot (position, view, blends, hp).</summary>
public struct SnapshotPlayer
{
	public byte NetId;
	public byte Flags;
	public Vector3 Pos;
	public Vector3 Vel;
	public float Yaw;
	public float Pitch;
	public byte AdsBlend;
	public byte CrouchBlend;
	public byte RaiseBlend;
	public ushort ShotIndex;
	public byte Hp;
	/// <summary>Kevlar 0..50. Consumed without regen; headshots bypass it.</summary>
	public byte Armor;
	public byte ActiveSlot;
	public byte WeaponId;
	public sbyte AimPunchX;
	public sbyte AimPunchY;
	public ushort FootstepPhase;
	public byte Kills;
	public byte Deaths;
	public byte PingMs;
	/// <summary>Team enum cast; drives puppet team-glow + scoreboard colour. None=0/CT=1/T=2/Deathmatch=3.</summary>
	public byte Team;
	/// <summary>Persistent per-team index (0..15), assigned at register time. Picks the player colour (palette[teamSlot]).</summary>
	public byte TeamSlot;
}
