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

namespace Vantix.Weapon;

/// <summary>Applies the weapon-bone (ik_hand_gun) offset after the AnimationMixer writes the skeleton.</summary>
[Tool, GlobalClass]
public partial class WeaponBoneModifier : SkeletonModifier3D
{
	[Export] public StringName BoneName = "ik_hand_gun";

	private int _boneIdx = -1;

	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		if ((string)property["name"] == "BoneName")
		{
			var skel = GetSkeleton();
			if (skel != null)
			{
				var names = new Godot.Collections.Array<string>();
				for (var i = 0; i < skel.GetBoneCount(); i++)
					names.Add(skel.GetBoneName(i));
				property["hint"] = (int)PropertyHint.Enum;
				property["hint_string"] = string.Join(",", names);
			}
		}
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		var skel = GetSkeleton();
		if (skel == null) return;
		using var _prof = MiniProfiler.SampleClient("WeaponBoneModifier._ProcessModification");

		// Procedural weapon offset (ADS/crouch/canted/recoil); grip bones ride it so both hands follow. Runs in-editor for ADS preview.
		if (Transform == Transform3D.Identity) return;
		if (_boneIdx < 0) _boneIdx = skel.FindBone(BoneName);
		if (_boneIdx < 0) return;
		var pose = new Transform3D(new Basis(skel.GetBonePoseRotation(_boneIdx)), skel.GetBonePosePosition(_boneIdx));
		var result = pose * Transform;
		skel.SetBonePosePosition(_boneIdx, result.Origin);
		skel.SetBonePoseRotation(_boneIdx, result.Basis.GetRotationQuaternion());
	}
}
