namespace Vantix.Character;

/// <summary>An input change at a fractional position within a tick, for subtick movement replay.</summary>
public struct SubtickEvent
{
	/// <summary>Position inside the tick, 0..1 = tick-start..tick-end. Events must be sorted ascending.</summary>
	public float TFraction;

	/// <summary>Held-state bitmask after this event applies, until the next event.</summary>
	public InputBits StateAfter;

	/// <summary>View yaw for the substep starting here.</summary>
	public float ViewYaw;

	/// <summary>View pitch for the substep starting here.</summary>
	public float ViewPitch;
}
