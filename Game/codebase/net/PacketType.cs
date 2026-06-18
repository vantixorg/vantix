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

namespace Vantix.Net;

/// <summary>Wire id per packet type. Keep values stable; bump Packets.ProtocolVersion on any incompatible
/// wire change.</summary>
public enum PacketType : byte
{
	ConnectRequest = 10,
	RespawnRequest = 11,
	ConVarSyncRequest = 12,
	WorldInitComplete = 13,
	TeamSelect = 14,
	SpawnAuthorize = 42,

	SpawnAck = 20,
	PlayerJoined = 21,
	PlayerLeft = 22,
	PlayerDisconnected = 23,
	PlayerReconnected = 24,
	ShotFired = 25,
	Reload = 26,
	GrenadeSpawn = 27,
	Footstep = 28,
	Hit = 29,
	Death = 30,
	Respawn = 31,
	SlotSwitch = 32,
	Jump = 33,
	Land = 34,
	Inspect = 35,
	DryFire = 36,
	SlideStart = 37,
	SlideEnd = 38,
	RoundState = 39,
	ProjectileDespawn = 40,
	ConVarSyncBroadcast = 41,
	DropMag = 43,
	GlassShatter = 44,

	Input = 50,

	Snapshot = 70,
	ProjectileState = 71,
	DebugHitboxes = 72,
	ServerLog = 73,
}
