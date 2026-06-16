using Godot;

namespace Vantix.Character;

/// <summary>
/// Wraps the FootstepAudio + WeaponAudio banks so callers don't touch the nodes directly.
/// Server scenes lack these nodes (null banks); all PlayX methods are null-safe.
/// </summary>
public sealed class PlayerAudio
{
	private readonly FootstepAudio _footsteps;
	private readonly WeaponAudio _weapon;

	public PlayerAudio(FootstepAudio footsteps, WeaponAudio weapon)
	{
		_footsteps = footsteps;
		_weapon = weapon;
	}

	/// <summary>Forwards IsLocalPlayer to both banks and preloads the active weapon.</summary>
	public void Configure(bool isLocalPlayer, WeaponStats activeWeapon)
	{
		if (_footsteps != null) _footsteps.IsLocalPlayer = isLocalPlayer;
		if (_weapon != null)
		{
			_weapon.IsLocalPlayer = isLocalPlayer;
			_weapon.Preload(activeWeapon);
		}
	}

	/// <summary>Footstep sound at pos.</summary>
	public void PlayStep(Vector3 pos, StringName material, float loud01, bool inTunnel, bool sprinting)
		=> _footsteps?.PlayStep(pos, material, loud01, inTunnel, sprinting);

	/// <summary>Jump-takeoff footstep.</summary>
	public void PlayJump(Vector3 pos, StringName material, float loud01, bool inTunnel)
		=> _footsteps?.PlayJump(pos, material, loud01, inTunnel);

	/// <summary>Landing footstep, scaled by impact.</summary>
	public void PlayLand(Vector3 pos, StringName material, float impact01, bool inTunnel)
		=> _footsteps?.PlayLand(pos, material, impact01, inTunnel);

	/// <summary>Shoot sound at the muzzle, with reverb env.</summary>
	public void PlayShoot(WeaponStats weapon, Vector3 muzzlePos, ReverbEnv env)
		=> _weapon?.PlayShoot(weapon, muzzlePos, env);

	/// <summary>Empty-magazine dry-fire click.</summary>
	public void PlayDryFire(WeaponStats weapon, Vector3 muzzlePos)
		=> _weapon?.PlayDryFire(weapon, muzzlePos);

	/// <summary>Reload sound.</summary>
	public void PlayReload(WeaponStats weapon, Vector3 muzzlePos)
		=> _weapon?.PlayReload(weapon, muzzlePos);
}
