namespace Vantix.Character;

/// <summary>
/// Hitbox zone for damage routing; keys WeaponStats.Damages and serialised as a byte in HitEvent packets.
/// Append-only — reordering breaks the wire protocol.
/// </summary>
public enum HitboxGroup : byte
{
	Body = 0,
	Head = 1,
	Chest = 2,
	Waist = 3,
	Arm = 4,
	Leg = 5,
	Hand = 6,
	Foot = 7,
}
