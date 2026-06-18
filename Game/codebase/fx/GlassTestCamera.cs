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

[GlobalClass]
public partial class GlassTestCamera : CharacterBody3D
{
	[Export] public float MoveSpeed = 6.0f;
	[Export] public float SprintMultiplier = 2.5f;
	[Export] public float MouseSensitivity = 0.0025f;
	[Export] public float RayLength = 200.0f;

	private const int WorldLayer = 1;
	private const int GlassLayer = 32;

	private Camera3D _camera;
	private float _yaw;
	private float _pitch;

	private void Fire()
	{
		var space = GetWorld3D().DirectSpaceState;
		Vector3 from = _camera.GlobalPosition;
		Vector3 dir = -_camera.GlobalTransform.Basis.Z;
		var query = PhysicsRayQueryParameters3D.Create(from, from + dir * RayLength);
		query.CollisionMask = WorldLayer | GlassLayer;
		var hit = space.IntersectRay(query);
		if (hit.Count == 0) return;

		if (hit["collider"].As<GodotObject>() is GlassPane glass)
		{
			Vector3 point = hit["position"].AsVector3();
			glass.RequestShatter(point, dir);
		}
	}

	private void ResetGlass()
	{
		foreach (var node in GetTree().GetNodesInGroup("glass"))
			if (node is GlassPane glass)
				glass.ResetPane();
	}

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera");
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_yaw = Rotation.Y;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw -= motion.Relative.X * MouseSensitivity;
			_pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.4f, 1.4f);
			Rotation = new Vector3(0f, _yaw, 0f);
			_camera.Rotation = new Vector3(_pitch, 0f, 0f);
		}

		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
			Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
				? Input.MouseModeEnum.Visible
				: Input.MouseModeEnum.Captured;

		if (@event.IsActionPressed("fire") && Input.MouseMode == Input.MouseModeEnum.Captured)
			Fire();

		if (@event.IsActionPressed("reload"))
			ResetGlass();
	}

	public override void _PhysicsProcess(double delta)
	{
		Basis cam = _camera.GlobalTransform.Basis;
		Vector3 input = Vector3.Zero;
		if (Input.IsActionPressed("forward")) input -= cam.Z;
		if (Input.IsActionPressed("backward")) input += cam.Z;
		if (Input.IsActionPressed("left")) input -= cam.X;
		if (Input.IsActionPressed("right")) input += cam.X;
		if (Input.IsActionPressed("jump")) input += Vector3.Up;
		if (Input.IsActionPressed("crouch")) input -= Vector3.Up;

		float speed = Input.IsActionPressed("run") ? MoveSpeed * SprintMultiplier : MoveSpeed;
		Velocity = input == Vector3.Zero ? Vector3.Zero : input.Normalized() * speed;
		MoveAndSlide();
	}
}
