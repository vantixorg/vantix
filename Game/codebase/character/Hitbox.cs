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
/// Scene-node hitbox dropped under a BoneAttachment3D. HitboxRig scans the skeleton in _Ready and
/// sets the layer / self-exclude RIDs. Group routes damage via WeaponStats.Damages.
/// </summary>
[Tool, GlobalClass]
public partial class Hitbox : StaticBody3D
{
	/// <summary>Zone (Head/Chest/Arm/...); keys the WeaponStats.Damages lookup.</summary>
	[Export] public HitboxGroup Group = HitboxGroup.Body;

	/// <summary>Sets collision layer/mask and joins the "flesh" group.</summary>
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		CollisionLayer = HitboxRig.Layer;
		CollisionMask = 0u;
		if (!IsInGroup("flesh")) AddToGroup("flesh");
	}
}
