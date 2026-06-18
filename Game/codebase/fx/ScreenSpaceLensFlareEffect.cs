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

/// <summary>Procedural screen-space lens flare. Finds the bright pixels in the frame and builds ghosts, a halo,
/// an anamorphic streak and a starburst from them — each with its own aperture-bokeh blur — then composites them
/// back additively with lens dirt. Post-transparent compositor pass over low-res buffers, needs TAA (not MSAA).</summary>
[Tool]
[GlobalClass]
public partial class ScreenSpaceLensFlareEffect : CompositorEffect
{
	private RenderingDevice _rd;
	private Rid _shader;
	private Rid _pipeline;
	private Rid _sampler;
	private Rid _linearSampler;
	private Rid _dirtDummy;
	private Rid _dirtTex;
	private Texture2D _dirtSource;
	private bool _dirtLogged;

	private RDUniform _u0, _u1, _u2, _u3, _u4;
	private Godot.Collections.Array<RDUniform> _uniformList;
	private readonly float[] _pushFloats = new float[32];
	private readonly byte[][] _pushBytes =
		{ new byte[128], new byte[128], new byte[128], new byte[128], new byte[128], new byte[128], new byte[128] };
	private readonly Rid[] _sets = new Rid[7];
	private static readonly int[] _passModes = { 0, 1, 1, 1, 2, 3, 4 };

	private readonly System.Collections.Generic.Dictionary<ulong, bool> _storageCheckCache
		= new System.Collections.Generic.Dictionary<ulong, bool>(4);
	private readonly System.Collections.Generic.Dictionary<(ulong, ulong, ulong, ulong, ulong), Rid> _setCache
		= new System.Collections.Generic.Dictionary<(ulong, ulong, ulong, ulong, ulong), Rid>(16);
	private readonly StringName _context = "ScreenSpaceLensFlare";
	private StringName _brightName = "ssfl_bright_4";
	private StringName _featName = "ssfl_features_4";
	private StringName _streakName = "ssfl_streak_4";
	private StringName _blurName = "ssfl_blur_4";
	private StringName _hblurName = "ssfl_hblur_4";
	private StringName _sblurName = "ssfl_sblur_4";
	private int _namedFactor = 4;

	[ExportGroup("Bright Pass")]
	[Export(PropertyHint.Range, "0.0,16.0,0.1")] public float Threshold = 3.5f;
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float Intensity = 0.22f;
	[Export(PropertyHint.Range, "0.5,32.0,0.5")] public float BrightCap = 4.0f;
	[Export(PropertyHint.Range, "0.05,4.0,0.05")] public float MaxFlare = 0.6f;
	// Iris blades: the pre-blur kernel is an N-gon so bright points become polygonal bokeh (6 = hexagon).
	[Export(PropertyHint.Range, "3,12,1")] public int ApertureBlades = 6;
	[Export] public Color Tint = new Color(1.0f, 0.95f, 0.88f);
	[Export(PropertyHint.Enum, "Half:2,Quarter:4,Eighth:8")] public int DownsampleFactor = 4;

	[ExportGroup("Ghosts")]
	[Export] public bool Ghosts = true;
	[Export(PropertyHint.Range, "0,16,1")] public int GhostCount = 6;
	// Blur radius of each feature's source (low-res texels). 0 = sharp.
	[Export(PropertyHint.Range, "0.0,32.0,0.5")] public float GhostBlur = 9.0f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float Dispersal = 0.32f;
	[Export(PropertyHint.Range, "0.0,0.05,0.0005")] public float ChromaticAmount = 0.011f;

	[ExportGroup("Halo")]
	[Export] public bool Halo = true;
	[Export(PropertyHint.Range, "0.0,32.0,0.5")] public float HaloBlur = 6.0f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float HaloWidth = 0.4f;
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float HaloStrength = 0.25f;

	[ExportGroup("Streak")]
	[Export] public bool Streak = true;
	[Export(PropertyHint.Range, "0.0,32.0,0.5")] public float StreakBlur = 4.0f;
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float StreakStrength = 0.5f;
	[Export(PropertyHint.Range, "0.0,1.0,0.005")] public float StreakLength = 0.22f;
	[Export] public Color StreakTint = new Color(0.7f, 0.8f, 1.0f);
	[Export(PropertyHint.Range, "0.0,2.0,0.01")] public float CrossStrength = 0.25f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float CrossLength = 0.35f;

	[ExportGroup("Starburst")]
	[Export] public bool Starburst = true;
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float StarburstStrength = 0.35f;
	[Export(PropertyHint.Range, "0,8,1")] public int StarburstPoints = 4;

	[ExportGroup("Lens Dirt")]
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float DirtStrength = 0.5f;
	[Export] public Texture2D DirtTexture;

	private static bool IsHeadless() =>
		OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless";

	private void InitializeCompute()
	{
		_rd = RenderingServer.GetRenderingDevice();
		if (_rd == null)
		{
			GD.PrintErr("[SSLensFlare] RenderingServer.GetRenderingDevice() returned null — effect will NOT run");
			return;
		}
		var shaderFile = GD.Load<RDShaderFile>("res://shaders/screen_space_lens_flare.glsl");
		if (shaderFile == null)
		{
			GD.PrintErr("[SSLensFlare] screen_space_lens_flare.glsl could not be loaded — check export filters for *.glsl");
			return;
		}
		var spirv = shaderFile.GetSpirV();
		string compileErr = spirv.GetStageCompileError(RenderingDevice.ShaderStage.Compute);
		if (!string.IsNullOrEmpty(compileErr))
			GD.PrintErr($"[SSLensFlare] SHADER COMPILE ERROR:\n{compileErr}");
		_shader = _rd.ShaderCreateFromSpirV(spirv);
		if (_shader.IsValid)
			_pipeline = _rd.ComputePipelineCreate(_shader);
		else
			GD.PrintErr("[SSLensFlare] Shader RID invalid — pipeline not created, effect is a no-op");

		_sampler = _rd.SamplerCreate(new RDSamplerState());
		_linearSampler = _rd.SamplerCreate(new RDSamplerState
		{
			MinFilter = RenderingDevice.SamplerFilter.Linear,
			MagFilter = RenderingDevice.SamplerFilter.Linear,
			RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
			RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
		});
		_dirtDummy = CreateDummyTexture();

		_u0 = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 0 };
		_u1 = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = 1 };
		_u2 = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 2 };
		_u3 = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 3 };
		_u4 = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 4 };
		_uniformList = new Godot.Collections.Array<RDUniform> { _u0, _u1, _u2, _u3, _u4 };
	}

	private Rid CreateDummyTexture()
	{
		var fmt = new RDTextureFormat
		{
			Format = RenderingDevice.DataFormat.R16G16B16A16Sfloat,
			Width = 1,
			Height = 1,
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit,
		};
		// 4 half-floats = 1.0 (0x3C00) little-endian -> white
		var data = new byte[] { 0x00, 0x3C, 0x00, 0x3C, 0x00, 0x3C, 0x00, 0x3C };
		return _rd.TextureCreate(fmt, new RDTextureView(), new Godot.Collections.Array<byte[]> { data });
	}

	private void EnsureNames(int f)
	{
		if (f == _namedFactor)
			return;
		_namedFactor = f;
		_brightName = $"ssfl_bright_{f}";
		_featName = $"ssfl_features_{f}";
		_streakName = $"ssfl_streak_{f}";
		_blurName = $"ssfl_blur_{f}";
		_hblurName = $"ssfl_hblur_{f}";
		_sblurName = $"ssfl_sblur_{f}";
		_setCache.Clear();
	}

	private void EnsureTex(RenderSceneBuffersRD b, StringName name, Vector2I sz, uint views)
	{
		if (!b.HasTexture(_context, name))
			b.CreateTexture(_context, name, RenderingDevice.DataFormat.R16G16B16A16Sfloat,
				(uint)(RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.SamplingBit),
				RenderingDevice.TextureSamples.Samples1, sz, views, 1, true, false);
	}

	private Rid ResolveDirt()
	{
		Texture2D tex = DirtTexture;
		if (tex == null)
			return _dirtDummy;
		if (tex != _dirtSource || !_dirtTex.IsValid)
		{
			if (_dirtTex.IsValid)
				_rd.FreeRid(_dirtTex);
			_dirtTex = CreateRdTextureFromImage(tex);
			_dirtSource = tex;
			if (!_dirtLogged)
			{
				_dirtLogged = true;
				GD.Print($"[SSLensFlare] dirt '{tex.ResourcePath}' -> RD texture valid={_dirtTex.IsValid}");
			}
		}
		return _dirtTex.IsValid ? _dirtTex : _dirtDummy;
	}

	private Rid CreateRdTextureFromImage(Texture2D tex)
	{
		Image img = tex.GetImage();
		if (img == null)
			return new Rid();
		if (img.IsCompressed())
			img.Decompress();
		if (img.GetFormat() != Image.Format.Rgba8)
			img.Convert(Image.Format.Rgba8);
		if (img.HasMipmaps())
			img.ClearMipmaps();
		var fmt = new RDTextureFormat
		{
			Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
			Width = (uint)img.GetWidth(),
			Height = (uint)img.GetHeight(),
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit,
		};
		return _rd.TextureCreate(fmt, new RDTextureView(), new Godot.Collections.Array<byte[]> { img.GetData() });
	}

	private Rid GetOrCreateSet(Rid s0, Rid t0, Rid t1, Rid s2, Rid t2, Rid t3, Rid t4)
	{
		var key = (t0.Id, t1.Id, t2.Id, t3.Id, t4.Id);
		if (_setCache.TryGetValue(key, out var cached))
		{
			if (_rd.UniformSetIsValid(cached))
				return cached;
			_setCache.Remove(key);
		}
		_u0.ClearIds(); _u0.AddId(s0); _u0.AddId(t0);
		_u1.ClearIds(); _u1.AddId(t1);
		_u2.ClearIds(); _u2.AddId(s2); _u2.AddId(t2);
		_u3.ClearIds(); _u3.AddId(_linearSampler); _u3.AddId(t3);
		_u4.ClearIds(); _u4.AddId(_linearSampler); _u4.AddId(t4);
		Rid fresh = _rd.UniformSetCreate(_uniformList, _shader, 0);
		_setCache[key] = fresh;
		return fresh;
	}

	private void PackPush(int slot, int mode, Vector2I size, Vector2I lo, bool hasDirt, float blur)
	{
		var f = _pushFloats;
		f[0] = size.X; f[1] = size.Y;
		f[2] = lo.X; f[3] = lo.Y;
		f[4] = mode;
		f[5] = Threshold;
		f[6] = Intensity;
		f[7] = Ghosts ? GhostCount : 0f;
		f[8] = Dispersal;
		f[9] = ChromaticAmount;
		f[10] = HaloWidth;
		f[11] = Halo ? HaloStrength : 0f;
		f[12] = Streak ? StreakStrength : 0f;
		f[13] = StreakLength;
		f[14] = DirtStrength;
		f[15] = hasDirt ? 1.0f : 0.0f;
		f[16] = BrightCap;
		f[17] = MaxFlare;
		f[18] = blur;
		f[19] = Starburst ? StarburstStrength : 0f;
		f[20] = Tint.R; f[21] = Tint.G; f[22] = Tint.B; f[23] = ApertureBlades;
		// tint.w / streak_tint.w are unused by the colour math -> carry ApertureBlades / StarburstPoints.
		f[24] = StreakTint.R; f[25] = StreakTint.G; f[26] = StreakTint.B; f[27] = StarburstPoints;
		f[28] = Streak ? CrossStrength : 0f;
		f[29] = CrossLength;
		System.Buffer.BlockCopy(f, 0, _pushBytes[slot], 0, 128);
	}

	public ScreenSpaceLensFlareEffect()
	{
		EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
		AccessResolvedColor = true;
		if (!IsHeadless())
			RenderingServer.CallOnRenderThread(Callable.From(InitializeCompute));
	}

	public override void _Notification(int what)
	{
		if (what == NotificationPredelete && _rd != null)
		{
			if (_pipeline.IsValid)
				_rd.FreeRid(_pipeline);
			if (_shader.IsValid)
				_rd.FreeRid(_shader);
			if (_sampler.IsValid)
				_rd.FreeRid(_sampler);
			if (_linearSampler.IsValid)
				_rd.FreeRid(_linearSampler);
			if (_dirtDummy.IsValid)
				_rd.FreeRid(_dirtDummy);
			if (_dirtTex.IsValid)
				_rd.FreeRid(_dirtTex);
		}
	}

	public override void _RenderCallback(int effectCallbackType, RenderData renderData)
	{
		if (_rd == null || !_pipeline.IsValid || Intensity <= 0.0f)
			return;
		if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers)
			return;

		Vector2I size = buffers.GetInternalSize();
		if (size.X == 0 || size.Y == 0)
			return;

		int f = DownsampleFactor <= 0 ? 4 : DownsampleFactor;
		EnsureNames(f);
		Vector2I lo = new Vector2I((size.X + f - 1) / f, (size.Y + f - 1) / f);
		uint fxG = ((uint)size.X + 15) / 16, fyG = ((uint)size.Y + 15) / 16;
		uint lxG = ((uint)lo.X + 15) / 16, lyG = ((uint)lo.Y + 15) / 16;
		uint views = buffers.GetViewCount();

		Rid dirt = ResolveDirt();
		bool hasDirt = DirtStrength > 0.0f && dirt.Id != _dirtDummy.Id;

		for (uint view = 0; view < views; view++)
		{
			Rid color = buffers.GetColorLayer(view);
			if (!_storageCheckCache.TryGetValue(color.Id, out bool isStorage))
			{
				isStorage = (_rd.TextureGetFormat(color).UsageBits
					& RenderingDevice.TextureUsageBits.StorageBit) != 0;
				_storageCheckCache[color.Id] = isStorage;
			}
			if (!isStorage)
				return;

			EnsureTex(buffers, _brightName, lo, views);
			EnsureTex(buffers, _blurName, lo, views);
			EnsureTex(buffers, _hblurName, lo, views);
			EnsureTex(buffers, _sblurName, lo, views);
			EnsureTex(buffers, _featName, lo, views);
			EnsureTex(buffers, _streakName, lo, views);
			Rid bright = buffers.GetTextureSlice(_context, _brightName, view, 0, 1, 1);
			Rid gblur = buffers.GetTextureSlice(_context, _blurName, view, 0, 1, 1);
			Rid hblur = buffers.GetTextureSlice(_context, _hblurName, view, 0, 1, 1);
			Rid sblur = buffers.GetTextureSlice(_context, _sblurName, view, 0, 1, 1);
			Rid feat = buffers.GetTextureSlice(_context, _featName, view, 0, 1, 1);
			Rid streak = buffers.GetTextureSlice(_context, _streakName, view, 0, 1, 1);

			// P0 bright | P1-3 blur (ghost/halo/streak radii) | P4 ghosts+halo | P5 streak | P6 composite.
			_sets[0] = GetOrCreateSet(_linearSampler, color, bright, _sampler, color, dirt, gblur);
			_sets[1] = GetOrCreateSet(_linearSampler, bright, gblur, _linearSampler, bright, dirt, gblur);
			_sets[2] = GetOrCreateSet(_linearSampler, bright, hblur, _linearSampler, bright, dirt, gblur);
			_sets[3] = GetOrCreateSet(_linearSampler, bright, sblur, _linearSampler, bright, dirt, gblur);
			_sets[4] = GetOrCreateSet(_linearSampler, gblur, feat, _linearSampler, hblur, dirt, gblur);
			_sets[5] = GetOrCreateSet(_linearSampler, sblur, streak, _linearSampler, sblur, dirt, gblur);
			_sets[6] = GetOrCreateSet(_linearSampler, feat, color, _linearSampler, streak, dirt, gblur);
			foreach (var set in _sets)
				if (!set.IsValid)
					return;

			long list = _rd.ComputeListBegin();
			_rd.ComputeListBindComputePipeline(list, _pipeline);
			for (int pass = 0; pass < 7; pass++)
			{
				if (pass > 0)
					_rd.ComputeListAddBarrier(list);
				float blur = pass == 1 ? GhostBlur : pass == 2 ? HaloBlur : pass == 3 ? StreakBlur : 0f;
				PackPush(pass, _passModes[pass], size, lo, hasDirt, blur);
				_rd.ComputeListBindUniformSet(list, _sets[pass], 0);
				_rd.ComputeListSetPushConstant(list, _pushBytes[pass], 128);
				bool full = pass == 6;
				_rd.ComputeListDispatch(list, full ? fxG : lxG, full ? fyG : lyG, 1);
			}
			_rd.ComputeListEnd();
		}
	}
}
