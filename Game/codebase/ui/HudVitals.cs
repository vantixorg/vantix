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

namespace Vantix.UI;

/// <summary>
/// Bottom-left vitals strip (no background): health number, armor icon + number,
/// a health bar, and a thinner stamina bar.
/// </summary>
public partial class HudVitals : Control
{
	public int Health = 100;
	public int Armor = 100;
	public int Stamina = 100;
	public bool StaminaExhausted;

	public const float StripW = 290f;
	public const float StripH = 58f;

	private static readonly Color Shadow = new(0f, 0f, 0f, 0.72f);
	private static readonly Color Track = new(0f, 0f, 0f, 0.46f);
	private static readonly Color HealthBar = new("c00201", 0.95f);
	private static readonly Color StaminaBar = new("2196f3", 0.7f);
	private static readonly Color Alarm = new(1f, 0.42f, 0.32f, 0.97f);
	private static readonly Color TextWhite = new(1f, 1f, 1f, 0.96f);
	private static readonly Color ArmorText = new(1f, 1f, 1f, 0.80f);
	private static readonly Color IconDim = new(1f, 1f, 1f, 0.62f);

	/// <summary>Draws the cross icon, health and armor numbers, and the two bars.</summary>
	public override void _Draw()
	{
		if (Size.X <= 1f) return;
		Font font = GetThemeDefaultFont();

		bool hpLow = Health <= 25;
		Color hpCol = hpLow ? Alarm : TextWhite;
		const float iconCy = 15f;

		float x = DrawNumber(font, Health.ToString(), 2f, iconCy, 34, hpCol);

		x += 16f;
		DrawIconCentered(KevlarTex, new Vector2(x + 10f, iconCy), 22f, IconDim);
		x += 26f;
		DrawNumber(font, Armor.ToString(), x, iconCy, 22, ArmorText);

		DrawSolidBar(new Rect2(2f, 44f, 224f, 6f), Health / 100f, hpLow ? Alarm : new Color(HealthBar, 0.7f));
		DrawSolidBar(new Rect2(2f, 53f, 224f, 3f),
			StaminaExhausted ? 0f : Stamina / 100f, StaminaExhausted ? Alarm : StaminaBar);
	}

	/// <summary>Draws a solid bar: dark track with a proportional fill, no segmenting.</summary>
	private void DrawSolidBar(Rect2 r, float frac, Color col)
	{
		frac = Mathf.Clamp(frac, 0f, 1f);
		DrawRect(r, Track);
		if (frac > 0f)
			DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * frac, r.Size.Y)), col);
	}

	private Texture2D _kevlarTex;
	private Texture2D KevlarTex => _kevlarTex ??= GD.Load<Texture2D>("res://assets/ui/kevlar.png");

	/// <summary>Draws an icon fitted into maxSize (aspect-preserving), centered, with a drop shadow. mod tints color + alpha.</summary>
	private void DrawIconCentered(Texture2D tex, Vector2 center, float maxSize, Color mod)
	{
		if (tex == null)
			return;
		float scale = maxSize / Mathf.Max(tex.GetWidth(), tex.GetHeight());
		float w = tex.GetWidth() * scale, h = tex.GetHeight() * scale;
		var rect = new Rect2(center.X - w * 0.5f, center.Y - h * 0.5f, w, h);
		DrawTextureRect(tex, new Rect2(rect.Position + Vector2.One, rect.Size), false, new Color(0f, 0f, 0f, mod.A * 0.6f));
		DrawTextureRect(tex, rect, false, mod);
	}

	/// <summary>Draws a shadowed number; (x, centerY) is the left edge / vertical center. Returns the right edge.</summary>
	private float DrawNumber(Font font, string text, float x, float centerY, int fs, Color col)
	{
		if (font == null) return x;
		Vector2 sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
		var pos = new Vector2(x, centerY + sz.Y * 0.32f);
		DrawString(font, pos + new Vector2(2f, 2f), text, HorizontalAlignment.Left, -1, fs, Shadow);
		DrawString(font, pos, text, HorizontalAlignment.Left, -1, fs, col);
		return x + sz.X;
	}
}
