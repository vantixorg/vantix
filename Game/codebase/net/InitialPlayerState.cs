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

/// <summary>One player's spawn state sent at join, before the first snapshot arrives.</summary>
public struct InitialPlayerState
{
	public byte NetId;
	public string PlayerName;
	public Vector3 Position;
	public float Yaw;
	public byte Hp;
	public byte ActiveSlot;
	public byte WeaponId;
	/// <summary>Team cast to byte; sent at join so puppets show team-glow before the first snapshot.</summary>
	public byte Team;
	/// <summary>See <see cref="SnapshotPlayer.TeamSlot"/>; sent at join for the right colour pre-snapshot.</summary>
	public byte TeamSlot;
}
