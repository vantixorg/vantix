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

/// <summary>Per-player field mask for delta-baseline snapshot compression. Bit 1 = send that group,
/// 0 = keep the baseline value. Groups bundle fields that change together (Pos+Vel, Yaw+Pitch).
/// All = full snapshot (player not in baseline). 13 bits fit a ushort.</summary>
[System.Flags]
public enum SnapshotFieldFlags : ushort
{
	None      = 0,
	Flags     = 1 << 0,
	Movement  = 1 << 1,  // Pos + Vel
	View      = 1 << 2,  // Yaw + Pitch
	Blends    = 1 << 3,  // AdsBlend + CrouchBlend + RaiseBlend
	ShotIndex = 1 << 4,
	Hp        = 1 << 5,
	Armor     = 1 << 6,
	Weapon    = 1 << 7,  // ActiveSlot + WeaponId
	AimPunch  = 1 << 8,  // AimPunchX + AimPunchY
	Footstep  = 1 << 9,
	Score     = 1 << 10, // Kills + Deaths
	Ping      = 1 << 11,
	Team      = 1 << 12, // Team + TeamSlot
	All       = (1 << 13) - 1,
}
