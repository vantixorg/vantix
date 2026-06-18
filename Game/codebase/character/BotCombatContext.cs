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

using System.Collections.Generic;
using Godot;

namespace Vantix.Character;

/// <summary>Per-tick world snapshot a bot reads to choose its movement and fire decisions.</summary>
public struct BotCombatContext
{
	public List<PeerState> AllPeers;
	public byte OwnNetId;
	public Team OwnTeam;
	public int Difficulty;
	public int TickRate;

	/// <summary>Magazine empty and not already reloading. Drives ReloadPressed; the edge fires the reload once.</summary>
	public bool NeedsReload;
}
