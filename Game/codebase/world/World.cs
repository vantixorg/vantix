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

namespace Vantix;

/// <summary>Root script of world.tscn. Owns the active map's Level registry and exposes it statically
/// (via Instance) to the HUD, bot AI, spawn system, and preview-camera cycler. LevelPath points at the
/// instanced map node; if unset, falls back to the first Level descendant.</summary>
[GlobalClass]
public partial class World : Node3D
{
	/// <summary>The live World root, or null between scene switches.</summary>
	public static World Instance { get; private set; }

	/// <summary>The active map's Level registry (lazily resolved), or null before the world is ready.</summary>
	public static Level Level => Instance?.ResolveLevel();

	/// <summary>Path to the instanced map root (the node carrying the Level script). Unset = auto-discover.</summary>
	[Export]
	public NodePath LevelPath { get; set; }

	private Level _level;

	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
		_level = null;
	}

	private Level ResolveLevel()
	{
		if (_level != null && GodotObject.IsInstanceValid(_level))
		{
			_level.EnsureResolved();
			return _level;
		}
		_level = (LevelPath != null && !LevelPath.IsEmpty) ? GetNodeOrNull<Level>(LevelPath) : null;
		_level ??= FindFirstLevel(this);
		if (_level == null)
		{
			GD.PushWarning("[World] No Level node found — set LevelPath on the World root to the map instance.");
			return null;
		}
		_level.EnsureResolved();
		return _level;
	}

	private static Level FindFirstLevel(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is Level l)
				return l;
			var nested = FindFirstLevel(child);
			if (nested != null)
				return nested;
		}
		return null;
	}
}
