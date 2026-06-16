namespace Vantix.Character;

/// <summary>Spectator camera mode for a PuppetPlayer: none, third-person follow, or first-person.</summary>
public enum SpectateMode
{
	/// <summary>No camera; puppet rendered in the world.</summary>
	None,
	/// <summary>Third-person follow camera.</summary>
	Tps,
	/// <summary>First-person through the puppet's eyes, no viewmodel.</summary>
	Fps,
}
