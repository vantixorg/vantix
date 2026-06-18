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

/// <summary>Bit flags packed into <see cref="SnapshotPlayer.Flags"/>.</summary>
[System.Flags]
public enum SnapshotFlags : byte
{
	None           = 0,
	Sliding        = 1 << 0,
	Airborne       = 1 << 1,
	Reloading      = 1 << 2,
	Sprinting      = 1 << 3,
	WallClinging   = 1 << 4,
	Inspecting     = 1 << 5,
	/// <summary>Client finished world preloads (WorldInitComplete); cleared on respawn/reconnect.
	/// Puppet TPS body stays hidden while unset.</summary>
	WorldReady     = 1 << 6,
	Dead           = 1 << 7,
}
