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

namespace Vantix.Fx;

/// <summary>Cosmetic physics bullet; launched with a velocity, hidden after its lifetime.</summary>
[GlobalClass]
public partial class Bullet : RigidBody3D
{
	public void Launch(Transform3D spawnTransform, Vector3 linearVelocity, Vector3 angularVelocity, float lifetime)
	{
		GlobalTransform = spawnTransform;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		Freeze = false;
		Visible = true;
		LinearVelocity = linearVelocity;
		AngularVelocity = angularVelocity;
		GetTree().CreateTimer(lifetime).Timeout += Reset;
	}

	public void Reset()
	{
		Visible = false;
		Freeze = true;
	}
}
