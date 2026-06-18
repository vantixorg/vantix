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

/// <summary>
/// Copies the level WorldEnvironment's look (tonemap, adjustment + LUT, glow, ambient tint/energy)
/// onto the viewmodel's own_world_3d Environment so the gun matches the map. Keeps the viewmodel's own
/// setup (Sky ambient/reflection from WorldCaptureRig, SSAO, the light rig). Called at the end of
/// Settings.Apply so it re-syncs whenever the world look changes (Bloom toggle, Brightness, etc.).
/// </summary>
public static class ViewmodelEnvSync
{
	/// <summary>Syncs viewmodel_env's look from the level WorldEnvironment. No-op if either env is missing.</summary>
	public static void Sync(SceneTree tree)
	{
		if (tree?.Root == null) return;
		Environment vm = FindViewmodelEnv(tree);
		Environment world = FindWorldEnv(tree);
		if (vm == null || world == null) return;

		vm.TonemapMode = world.TonemapMode;
		vm.TonemapExposure = world.TonemapExposure;
		vm.TonemapWhite = world.TonemapWhite;

		vm.AdjustmentEnabled = world.AdjustmentEnabled;
		vm.AdjustmentBrightness = world.AdjustmentBrightness;
		vm.AdjustmentContrast = world.AdjustmentContrast;
		vm.AdjustmentSaturation = world.AdjustmentSaturation;
		vm.AdjustmentColorCorrection = world.AdjustmentColorCorrection;

		vm.GlowEnabled = world.GlowEnabled;
		for (int i = 0; i < 7; i++) vm.SetGlowLevel(i, world.GetGlowLevel(i));
		vm.GlowNormalized = world.GlowNormalized;
		vm.GlowIntensity = world.GlowIntensity;
		vm.GlowStrength = world.GlowStrength;
		vm.GlowMix = world.GlowMix;
		vm.GlowBloom = world.GlowBloom;
		vm.GlowBlendMode = world.GlowBlendMode;
		vm.GlowHdrThreshold = world.GlowHdrThreshold;
		vm.GlowHdrScale = world.GlowHdrScale;
		vm.GlowHdrLuminanceCap = world.GlowHdrLuminanceCap;

		// Match ambient tint/energy but keep the viewmodel's Sky ambient source (fed by the capture rig).
		vm.AmbientLightColor = world.AmbientLightColor;
		vm.AmbientLightEnergy = world.AmbientLightEnergy;
	}

	/// <summary>The Environment of the own_world_3d SubViewport (the viewmodel env).</summary>
	private static Environment FindViewmodelEnv(SceneTree tree)
	{
		foreach (Node n in tree.Root.FindChildren("*", "SubViewport", true, false))
		{
			if (n is not SubViewport sv || !sv.OwnWorld3D) continue;
			foreach (Node c in sv.GetChildren())
				if (c is WorldEnvironment we && we.Environment != null) return we.Environment;
		}
		return null;
	}

	/// <summary>The level Environment (the compositor-bearing WorldEnvironment, not a viewmodel one), else the first non-viewmodel env.</summary>
	private static Environment FindWorldEnv(SceneTree tree)
	{
		Environment first = null;
		foreach (Node n in tree.Root.FindChildren("*", "WorldEnvironment", true, false))
		{
			if (n is not WorldEnvironment we || we.Environment == null) continue;
			if (ViewmodelMotionBlur.IsViewmodelEnvironment(we)) continue;
			if (we.Compositor != null) return we.Environment;
			first ??= we.Environment;
		}
		return first;
	}
}
