namespace Vantix.Character;

/// <summary>
/// Held-input bitfield for subtick movement; a bit is set while its key is down. No "pressed" bit:
/// the driver detects press-edges from the 0→1 transition between consecutive SubtickEvent.StateAfter masks.
/// </summary>
[System.Flags]
public enum InputBits : ushort
{
	None = 0,
	Forward = 1 << 0,
	Back = 1 << 1,
	Left = 1 << 2,
	Right = 1 << 3,
	Jump = 1 << 4,
	Crouch = 1 << 5,
	Sprint = 1 << 6,
	ShiftWalk = 1 << 7,
	Fire = 1 << 8,
	Ads = 1 << 9,
	Reload = 1 << 10,
	Inspect = 1 << 11,
	BreathHold = 1 << 12,
}
