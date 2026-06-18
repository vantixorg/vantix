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

/// <summary>Marks a child node as a weapon attachment of a given type and variant.</summary>
[Tool, GlobalClass]
public partial class WeaponAttachment : Node3D
{
	[Export] public AttachmentType Group;
	[Export] public AttachmentVariant Variant = AttachmentVariant.Default;
}
