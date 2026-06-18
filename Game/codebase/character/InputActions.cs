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

namespace Vantix.Utils;

/// <summary>
/// Godot InputMap action names the character reads. Edit in Project Settings → Input Map.
/// </summary>
public static class InputActions
{
	public static readonly StringName Forward = "forward";
	public static readonly StringName Back = "backward";
	public static readonly StringName Left = "left";
	public static readonly StringName Right = "right";
	public static readonly StringName Jump = "jump";
	public static readonly StringName Shift = "shift";
	public static readonly StringName Crouch = "crouch";
	public static readonly StringName Sprint = "run";
	public static readonly StringName Fire = "fire";
	public static readonly StringName Reload = "reload";
	public static readonly StringName Inspect = "inspect";
	public static readonly StringName Ads = "zoom";
	public static readonly StringName Breath = "breath";
	public static readonly StringName SlotWeapon = "slot_1";
	public static readonly StringName SlotGrenade = "slot_2";
	public static readonly StringName Console = "console";
	public static readonly StringName CameraSwitch = "camera_switch";
}
