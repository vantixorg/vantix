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

namespace Vantix.Utils;

/// <summary>Central input gate. Blocked is true when the player should get no game input: settings menu open,
/// window unfocused (Alt-Tab), mouse capture off, or LocalPlayer dead. Check it before reading input.</summary>
public static class InputGate
{
	/// <summary>True from LocalPlayer._Ready until WorldInitComplete is sent; freezes input reads and
	/// SendNetInput so the player can't move or send pre-spawn ticks while preloads run.</summary>
	public static bool LocalPlayerFrozen;

	private static readonly bool _headless = DisplayServer.GetName() == "headless";

	public static bool Blocked
	{
		get
		{
			if (LocalPlayerFrozen)
				return true;
			if (SettingsMenu.IsAnyOpen)
				return true;
			if (ConsoleHud.IsAnyOpen)
				return true;
			if (!_headless && !DisplayServer.WindowIsFocused(0))
				return true;
			var selfSnap = NetMain.Instance?.Client?.LastSelfSnap;
			if (selfSnap.HasValue && selfSnap.Value.Hp == 0)
				return true;
			return false;
		}
	}
}
