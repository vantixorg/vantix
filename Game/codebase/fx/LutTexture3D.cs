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
using Godot.Collections;

namespace Vantix.Fx;

/// <summary>
/// Converts a 2D horizontal LUT strip into an ImageTexture3D and assigns it to the parent
/// WorldEnvironment's color-correction slot.
/// </summary>
[Tool]
public partial class LutTexture3D : Node
{
	[Export] public Texture2D LutStrip;
	[Export] public int LutSize = 16;

	/// <summary>Builds the 3D LUT on load and assigns it to the parent WorldEnvironment.</summary>
	public override void _Ready()
	{
		if (LutStrip == null) return;
		var parent = GetParent<WorldEnvironment>();
		if (parent?.Environment == null) return;
		parent.Environment.AdjustmentColorCorrection = BuildTexture3D(LutStrip, LutSize);
	}

	/// <summary>Slices a horizontal LUT strip into N square slices to build an ImageTexture3D.</summary>
	public static ImageTexture3D BuildTexture3D(Texture2D strip, int size)
	{
		var src = strip.GetImage();
		if (src.IsCompressed())
			src.Decompress();
		if (src.GetWidth() != size * size || src.GetHeight() != size)
		{
			GD.PushError($"LutTexture3D: expected {size * size}x{size} strip, got {src.GetWidth()}x{src.GetHeight()}");
			return null;
		}

		var slices = new Array<Image>();
		for (int z = 0; z < size; z++)
		{
			var slice = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				slice.SetPixel(x, y, src.GetPixel(z * size + x, y));
			slices.Add(slice);
		}

		var tex = new ImageTexture3D();
		tex.Create(Image.Format.Rgb8, size, size, size, false, slices);
		return tex;
	}
}
