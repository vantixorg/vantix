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
