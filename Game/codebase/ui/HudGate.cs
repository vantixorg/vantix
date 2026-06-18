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

namespace Vantix.Utils;

/// <summary>Auto-hides the game-HUD layers (Hitmarker, Killfeed, Crosshair, HudCs2) while the local player
/// is frozen (team-select/preload) or dead. Meta UI (Console, NetGraph, Scoreboard, MiniProfiler) is not
/// gated. Tick refreshes registered HUDs once per frame.</summary>
public static class HudGate
{
	private static readonly List<Node> _items = new();

	/// <summary>Registers a HUD root (CanvasLayer or Control) for auto-hide. Idempotent; stale refs drop on next Tick.</summary>
	public static void Register(Node item)
	{
		if (item == null)
			return;
		if (_items.Contains(item))
			return;
		_items.Add(item);
	}

	/// <summary>Clears all registrations — called by NetMain on disconnect / scene-reload.</summary>
	public static void Reset() => _items.Clear();

	/// <summary>True when the game-HUD should show: a spawned, alive local player and no preload/team-select.</summary>
	public static bool ShouldShow
	{
		get
		{
			var client = NetMain.Instance?.Client;
			if (client == null)
				return false;
			if (!client.SpawnAuthorized)
				return false;
			if (InputGate.LocalPlayerFrozen)
				return false;
			var snap = client.LastSelfSnap;
			if (snap.HasValue && snap.Value.Hp == 0)
				return false;
			return true;
		}
	}

	/// <summary>Per-frame visibility refresh (from NetMain._PhysicsProcess). Drops invalid handles as it goes.</summary>
	public static void Tick()
	{
		bool show = ShouldShow;
		for (int i = _items.Count - 1; i >= 0; i--)
		{
			var item = _items[i];
			if (!GodotObject.IsInstanceValid(item))
			{
				_items.RemoveAt(i);
				continue;
			}
			switch (item)
			{
				case CanvasLayer cl:
					if (cl.Visible != show)
						cl.Visible = show;
					break;
				case CanvasItem ci:
					if (ci.Visible != show)
						ci.Visible = show;
					break;
			}
		}
	}
}
