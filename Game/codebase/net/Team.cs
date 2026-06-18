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
