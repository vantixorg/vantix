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

namespace Vantix.Levels;

/// <summary>
/// A respawn region (a Zone); players land at the area centre, or a sampled cell when several spawn
/// together. The Kind tag selects the mode/team pool, resolved by SpawnManager.
/// </summary>
[Tool, GlobalClass]
public partial class Spawn : Zone
{
	/// <summary>Spawn pool (deathmatch / team 1 / team 2) this region belongs to.</summary>
	public enum SpawnKind { Deathmatch, Team1, Team2 }

	/// <summary>Spawn pool (Deathmatch/Team1/Team2) this region belongs to.</summary>
	[Export] public SpawnKind Kind { get; set; } = SpawnKind.Deathmatch;
}
