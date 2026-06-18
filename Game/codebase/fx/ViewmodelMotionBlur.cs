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
/// Gives the weapon viewmodel its own Compositor + PostProcessEffect — its own_world_3d SubViewport
/// is invisible to the world Compositor and would otherwise get no post-processing. Configure mirrors
/// the world toggles and uses the same compositor-path gating so it never stacks with the FSR2 PostCanvasFx path.
/// </summary>
public static class ViewmodelMotionBlur
{
	/// <summary>Weaker than the world's — high muzzle angular velocity would otherwise smear the gun front.</summary>
	private const float WeaponBlurStrength = 1.2f;

	private static PostProcessEffect _effect;

	/// <summary>The per-viewmodel effect (null before Attach); exposed so the ADS feed can push AdsBlend onto the weapon pass.</summary>
	public static PostProcessEffect Effect => _effect;

	/// <summary>Attaches a Compositor + PostProcessEffect to the viewmodel WorldEnvironment. Calling twice replaces the previous one; call Configure after for real toggles.</summary>
	public static void Attach(Node localPlayer)
	{
		if (localPlayer == null) return;
		WorldEnvironment vmEnv = FindViewmodelEnvironment(localPlayer);
		if (vmEnv == null)
		{
			Dbg.Print("[ViewmodelMotionBlur] viewmodel WorldEnvironment not found — skipping");
			return;
		}
		var comp = new Compositor();
		_effect = new PostProcessEffect { MotionBlur = true, Enabled = true, MotionBlurStrength = WeaponBlurStrength };
		comp.CompositorEffects = new Godot.Collections.Array<CompositorEffect> { _effect };
		vmEnv.Compositor = comp;
		Dbg.Print("[ViewmodelMotionBlur] attached PostProcessEffect to viewmodel_env");
	}

	/// <summary>Mirrors the world toggles onto the viewmodel effect (no-op if not attached). enabled must follow the world's compositor-path gating so it never stacks on the FSR2 PostCanvasFx pass.</summary>
	public static void Configure(bool enabled, bool chromaticAberration, bool sharpening, bool vignette, bool filmGrain, bool motionBlur)
	{
		if (_effect == null) return;
		_effect.Enabled = enabled;
		_effect.ChromaticAberration = chromaticAberration;
		_effect.Sharpening = sharpening;
		_effect.Vignette = vignette;
		_effect.FilmGrain = filmGrain;
		_effect.MotionBlur = motionBlur;
	}

	/// <summary>Toggles the effect on/off. No-op if not yet attached.</summary>
	public static void SetEnabled(bool enabled)
	{
		if (_effect == null) return;
		_effect.Enabled = enabled;
	}

	/// <summary>Clears the stored reference so the next Attach starts fresh after a level/player reload.</summary>
	public static void Reset()
	{
		_effect = null;
	}

	/// <summary>True if the node lives inside an own_world_3d SubViewport (the viewmodel world); lets world-env finders skip the viewmodel's own Environment.</summary>
	public static bool IsViewmodelEnvironment(Node node)
	{
		for (Node n = node; n != null; n = n.GetParent())
			if (n is SubViewport sv && sv.OwnWorld3D) return true;
		return false;
	}

	/// <summary>Returns the WorldEnvironment child of the LocalPlayer's own_world_3d SubViewport (the viewmodel viewport).</summary>
	private static WorldEnvironment FindViewmodelEnvironment(Node root)
	{
		foreach (Node n in root.FindChildren("*", "SubViewport", true, false))
		{
			if (n is not SubViewport sv || !sv.OwnWorld3D) continue;
			foreach (Node child in sv.GetChildren())
			{
				if (child is WorldEnvironment we) return we;
			}
		}
		return null;
	}
}
