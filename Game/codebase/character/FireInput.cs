using Godot;

namespace Vantix.Character;

/// <summary>Per-tick weapon input (fire/reload/ads plus shooter state) for the fire logic.</summary>
public struct FireInput
{
	/// <summary>Server rewinds the world snapshot to this tick.</summary>
	public uint TickIndex;

	/// <summary>Mouse1 or fire key.</summary>
	public bool FirePressed;

	/// <summary>Held; MovementController detects the press edge.</summary>
	public bool ReloadPressed;

	/// <summary>Held; MovementController detects the press edge.</summary>
	public bool InspectPressed;

	public bool AdsHeld;

	/// <summary>Gameplay flag (e.g. Dead).</summary>
	public bool CanFire;
	public WeaponStats Weapon;

	/// <summary>Horizontal speed for spread scaling.</summary>
	public float Speed;

	/// <summary>Used by server-side lag compensation.</summary>
	public Vector3 ShooterPosition;

	public float ViewYaw;
	public float ViewPitch;
	public float Dt;

	/// <summary>Forward unit vector from yaw/pitch; server raycasts from ShooterPosition along this.</summary>
	public readonly Vector3 AimDirection
	{
		get
		{
			float cp = Mathf.Cos(ViewPitch);
			return new Vector3(-Mathf.Sin(ViewYaw) * cp, Mathf.Sin(ViewPitch), -Mathf.Cos(ViewYaw) * cp);
		}
	}
}
