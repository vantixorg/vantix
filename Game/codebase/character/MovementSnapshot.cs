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

namespace Vantix.Character;

/// <summary>
/// Full MovementController state for client-prediction reconciliation. Snapshotted per tick into a
/// ring buffer and restored before replay. Value type (no GC). Excludes Sv (immutable) and _fireRng
/// (re-seeded deterministically from TickIndex+ShotIndex). Node-side state (transform, mantle) lives
/// in the netcode snapshot.
/// </summary>
public struct MovementSnapshot
{
	public Vector3 Velocity;
	public float Stamina,
		CrouchBlend,
		StaminaRegenTimer;
	public bool SprintExhausted,
		SprintNeedsRelease;
	public int FireMode,
		ShotIndex;
	public float FireCooldown,
		TimeSinceLastShot,
		WeaponRaiseBlend,
		ReloadTimer,
		InspectTimer,
		AdsBlend;
	public bool FirePressedLast,
		ReloadPressedLast,
		InspectPressedLast;
	public Vector3 AimPunch;
	public int CurrentMag,
		ReserveAmmo,
		LastReloadMoved,
		PendingReloadIntoMag;
	public bool UnlimitedAmmo,
		ReloadWasActive;
	public float BreathHoldTimer,
		BreathRecoverTimer,
		BreathCooldownTimer;
	public bool BreathHoldActiveNow;
	public bool IsAirborne,
		CrouchCancelJumpUsed;
	public float PrevVelocityY,
		CoyoteTimer,
		JumpBufferTimer,
		TimeSinceJump,
		CrouchBufferTimer;
	public bool IsSliding;
	public float SlideTimer,
		SlideStopAccuracyTimer;
	public bool WallJumpAvailable,
		IsWallClinging;
	public float WallClingTimer,
		WallClingEntrySpeed,
		PreMoveHorizSpeed;
	public int WallClingChargesRemaining;
	public Vector3 LastWishDir,
		LastShotOrigin,
		LastShotDirection;
	public Vector2 LastShotPatternEntry;
	public float LastShotSpread;
	public bool ActuallySprinting,
		RecentlyFired,
		DidJumpThisFrame,
		DidWallJumpThisFrame,
		DidFireThisFrame,
		DidDryFireThisFrame,
		DidReloadThisFrame;
}
