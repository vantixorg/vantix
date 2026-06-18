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
using System.Collections.Generic;

namespace Vantix.Weapon;

/// <summary>Magazine prop: rides the socket while loaded, drops as a physics body when ejected; plays the depletion anim.</summary>
[Tool, GlobalClass]
public partial class AnimatedMagazin : RigidBody3D
{
	private static readonly HashSet<string> AnimProps = new()
	{
		nameof(MagazineDepletion),
	};

	[ExportGroup("Physics")]
	// False = in-socket (no collision); true = dropped mag.
	[Export] public bool CollisionEnabled = false;

	[ExportGroup("Animation Player")]
	[Export] public NodePath AnimationPlayerPath = new("MergedAnimationPlayer");

	[ExportGroup("Animations")]
	[Export] public StringName MagazineDepletion;

	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		if (!AnimProps.Contains((string)property["name"])) return;
		var player = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
		if (player == null) return;
		property["hint"] = (int)PropertyHint.Enum;
		property["hint_string"] = string.Join(",", player.GetAnimationList());
	}

	private AnimationPlayer _player;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		if (!CollisionEnabled)
		{
			// Freeze + no collision so it rides the socket without fighting the gun/hands.
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			CollisionLayer = 0;
			CollisionMask = 0;
			// Drive straight from the node; interpolation lags the socket-parented body and flickers.
			PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
		}
		_player = GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);
		if (_player != null && !string.IsNullOrEmpty(MagazineDepletion?.ToString()) && _player.HasAnimation(MagazineDepletion))
			SanitizeRotationTracks(_player.GetAnimation(MagazineDepletion));
		SetFill(1f);
	}

	public void SetFill(float fill01)
	{
		if (_player == null || string.IsNullOrEmpty(MagazineDepletion?.ToString()) || !_player.HasAnimation(MagazineDepletion)) return;
		var anim = _player.GetAnimation(MagazineDepletion);
		double len = anim?.Length ?? 0.0;
		if (len <= 0.0) return;
		_player.Play(MagazineDepletion);
		_player.Seek(Mathf.Clamp(1f - fill01, 0f, 1f) * len, true);
		_player.Pause();
	}

	private static void SanitizeRotationTracks(Animation anim)
	{
		if (anim == null) return;
		for (int t = 0; t < anim.GetTrackCount(); t++)
		{
			if (anim.TrackGetType(t) != Animation.TrackType.Rotation3D) continue;
			int keys = anim.TrackGetKeyCount(t);
			for (int k = 0; k < keys; k++)
			{
				Quaternion q = (Quaternion)anim.TrackGetKeyValue(t, k);
				if (q.IsNormalized()) continue;
				anim.TrackSetKeyValue(t, k, q.LengthSquared() > 1e-6f ? q.Normalized() : Quaternion.Identity);
			}
		}
	}
}
