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

[Tool]
[GlobalClass]
public partial class DustMotes : GpuParticles3D
{
	[ExportGroup("Emission Volume")]
	private Vector3 _boxExtents = new(55f, 14f, 55f);
	[Export] public Vector3 BoxExtents { get => _boxExtents; set { _boxExtents = value; QueueConfigure(); } }
	private float _spread = 180f;
	[Export(PropertyHint.Range, "0,180,1")] public float Spread { get => _spread; set { _spread = value; QueueConfigure(); } }
	private float _velocityMin = 0.01f;
	[Export(PropertyHint.Range, "0,2,0.001")] public float VelocityMin { get => _velocityMin; set { _velocityMin = value; QueueConfigure(); } }
	private float _velocityMax = 0.06f;
	[Export(PropertyHint.Range, "0,2,0.001")] public float VelocityMax { get => _velocityMax; set { _velocityMax = value; QueueConfigure(); } }
	private float _gravityY = -0.003f;
	[Export(PropertyHint.Range, "-1,1,0.001")] public float GravityY { get => _gravityY; set { _gravityY = value; QueueConfigure(); } }
	private float _turbulence = 0.12f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float Turbulence { get => _turbulence; set { _turbulence = value; QueueConfigure(); } }
	private float _turbulenceScale = 1.0f;
	[Export(PropertyHint.Range, "0.1,8,0.1")] public float TurbulenceScale { get => _turbulenceScale; set { _turbulenceScale = value; QueueConfigure(); } }

	[ExportGroup("Appearance")]
	private float _moteSize = 0.025f;
	[Export(PropertyHint.Range, "0.001,0.2,0.001")] public float MoteSize { get => _moteSize; set { _moteSize = value; QueueConfigure(); } }
	private float _scaleMin = 0.35f;
	[Export(PropertyHint.Range, "0.05,4,0.01")] public float ScaleMin { get => _scaleMin; set { _scaleMin = value; QueueConfigure(); } }
	private float _scaleMax = 0.9f;
	[Export(PropertyHint.Range, "0.05,4,0.01")] public float ScaleMax { get => _scaleMax; set { _scaleMax = value; QueueConfigure(); } }
	private Color _colorA = new(1f, 1f, 1f);
	[Export] public Color ColorA { get => _colorA; set { _colorA = value; QueueConfigure(); } }
	private Color _colorB = new(1f, 0.84f, 0.58f);
	[Export] public Color ColorB { get => _colorB; set { _colorB = value; QueueConfigure(); } }
	private float _brightness = 1.2f;
	[Export(PropertyHint.Range, "0,4,0.01")] public float Brightness { get => _brightness; set { _brightness = value; QueueConfigure(); } }
	private float _sparkle = 0.12f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float Sparkle { get => _sparkle; set { _sparkle = value; QueueConfigure(); } }
	private float _sparkleRatio = 0.3f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float SparkleRatio { get => _sparkleRatio; set { _sparkleRatio = value; QueueConfigure(); } }
	private float _twinkleSpeed = 2.2f;
	[Export(PropertyHint.Range, "0,10,0.1")] public float TwinkleSpeed { get => _twinkleSpeed; set { _twinkleSpeed = value; QueueConfigure(); } }

	[ExportGroup("Fade")]
	private float _softFade = 0.5f;
	[Export(PropertyHint.Range, "0,4,0.01")] public float SoftFade { get => _softFade; set { _softFade = value; QueueConfigure(); } }
	private float _nearFadeStart = 0.15f;
	[Export(PropertyHint.Range, "0,5,0.01")] public float NearFadeStart { get => _nearFadeStart; set { _nearFadeStart = value; QueueConfigure(); } }
	private float _nearFadeEnd = 0.7f;
	[Export(PropertyHint.Range, "0,5,0.01")] public float NearFadeEnd { get => _nearFadeEnd; set { _nearFadeEnd = value; QueueConfigure(); } }
	private float _farFadeStart = 28f;
	[Export(PropertyHint.Range, "1,200,1")] public float FarFadeStart { get => _farFadeStart; set { _farFadeStart = value; QueueConfigure(); } }
	private float _farFadeEnd = 55f;
	[Export(PropertyHint.Range, "1,200,1")] public float FarFadeEnd { get => _farFadeEnd; set { _farFadeEnd = value; QueueConfigure(); } }
	private float _lightResponse = 0.7f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float LightResponse { get => _lightResponse; set { _lightResponse = value; QueueConfigure(); } }

	private static Shader _shader;
	private static Shader DustShader => _shader ??= GD.Load<Shader>("res://shaders/dust.gdshader");

	private bool _configureQueued;

	public DustMotes()
	{
		Amount = 16000;
		Lifetime = 16.0;
		Preprocess = 16.0;
		Randomness = 1.0f;
		LocalCoords = false;
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		VisibilityAabb = new Aabb(new Vector3(-58f, -20f, -58f), new Vector3(116f, 45f, 116f));
	}

	public override void _Ready()
	{
		Configure();
	}

	private void QueueConfigure()
	{
		if (_configureQueued)
			return;
		_configureQueued = true;
		Callable.From(() => { _configureQueued = false; Configure(); }).CallDeferred();
	}

	private void Configure()
	{
		var pm = ProcessMaterial as ParticleProcessMaterial ?? new ParticleProcessMaterial();
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = _boxExtents;
		pm.Direction = new Vector3(0f, 1f, 0f);
		pm.Spread = _spread;
		pm.InitialVelocityMin = _velocityMin;
		pm.InitialVelocityMax = _velocityMax;
		pm.Gravity = new Vector3(0f, _gravityY, 0f);
		pm.TurbulenceEnabled = _turbulence > 0f;
		pm.TurbulenceNoiseStrength = _turbulence;
		pm.TurbulenceNoiseScale = _turbulenceScale;
		pm.ScaleMin = _scaleMin;
		pm.ScaleMax = _scaleMax;
		ProcessMaterial = pm;

		var quad = DrawPass1 as QuadMesh ?? new QuadMesh();
		var sm = quad.Material as ShaderMaterial ?? new ShaderMaterial();
		sm.Shader = DustShader;
		sm.SetShaderParameter("dust_color_a", _colorA);
		sm.SetShaderParameter("dust_color_b", _colorB);
		sm.SetShaderParameter("brightness", _brightness);
		sm.SetShaderParameter("sparkle", _sparkle);
		sm.SetShaderParameter("sparkle_ratio", _sparkleRatio);
		sm.SetShaderParameter("twinkle_speed", _twinkleSpeed);
		sm.SetShaderParameter("soft_fade", _softFade);
		sm.SetShaderParameter("near_fade_start", _nearFadeStart);
		sm.SetShaderParameter("near_fade_end", _nearFadeEnd);
		sm.SetShaderParameter("far_fade_start", _farFadeStart);
		sm.SetShaderParameter("far_fade_end", _farFadeEnd);
		sm.SetShaderParameter("light_response", _lightResponse);
		quad.Size = new Vector2(_moteSize, _moteSize);
		quad.Material = sm;
		DrawPass1 = quad;
	}

	public override void _Process(double delta)
	{
		bool on = Engine.IsEditorHint() || Settings.DustMotes;
		if (Visible != on)
			Visible = on;
		if (Emitting != on)
			Emitting = on;
	}
}
