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

// Depth-aware floating dust, main-world compositor only (viewmodel has its own compositor).
[Tool]
[GlobalClass]
public partial class DustMotesEffect : CompositorEffect
{
	private RenderingDevice _rd;
	private Rid _shader;
	private Rid _pipeline;
	private Rid _sampler;

	private RDUniform _colorUniform;
	private RDUniform _depthUniform;
	private Godot.Collections.Array<RDUniform> _uniformList;
	private readonly float[] _pushFloats = new float[32];
	private readonly byte[] _pushBytes = new byte[32 * sizeof(float)];

	private readonly System.Collections.Generic.Dictionary<ulong, bool> _storageCheckCache
		= new System.Collections.Generic.Dictionary<ulong, bool>(4);
	private readonly System.Collections.Generic.Dictionary<(ulong color, ulong depth), Rid> _setCache
		= new System.Collections.Generic.Dictionary<(ulong, ulong), Rid>(4);
	private int _firstRenderLogged;

	[Export(PropertyHint.Range, "0.0,8.0,0.01")] public float Brightness = 2.2f;
	[Export(PropertyHint.Range, "0.0,4.0,0.01")] public float Density = 0.8f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float Sparkle = 0.6f;
	[Export(PropertyHint.Range, "0.0,10.0,0.1")] public float TwinkleSpeed = 2.5f;
	[Export(PropertyHint.Range, "0.1,8.0,0.1")] public float NearFade = 0.5f;
	[Export(PropertyHint.Range, "5.0,200.0,1.0")] public float FarFade = 30.0f;
	[Export(PropertyHint.Range, "0.1,4.0,0.05")] public float CellSize = 1.0f;
	[Export(PropertyHint.Range, "0.004,0.1,0.002")] public float MoteRadius = 0.022f;
	[Export(PropertyHint.Range, "0.05,2.0,0.05")] public float StartDist = 0.3f;
	[Export(PropertyHint.Range, "0.5,0.99,0.005")] public float Coverage = 0.9f;

	private bool AnyEffectActive => Brightness > 0f && Density > 0f;

	private static bool IsHeadless() =>
		OS.HasFeature("dedicated_server") || DisplayServer.GetName() == "headless";

	private void InitializeCompute()
	{
		_rd = RenderingServer.GetRenderingDevice();
		if (_rd == null)
		{
			GD.PrintErr("[DustMotes] InitializeCompute: RenderingServer.GetRenderingDevice() returned null — local RD unavailable, dust pass will NOT run");
			return;
		}
		var shaderFile = GD.Load<RDShaderFile>("res://shaders/dust_motes.glsl");
		if (shaderFile == null)
		{
			GD.PrintErr("[DustMotes] dust_motes.glsl could not be loaded — file likely missing from export. Check export_presets.cfg include filters for *.glsl");
			return;
		}
		var spirv = shaderFile.GetSpirV();
		string compileErr = spirv.GetStageCompileError(RenderingDevice.ShaderStage.Compute);
		if (!string.IsNullOrEmpty(compileErr))
			GD.PrintErr($"[DustMotes] SHADER COMPILE ERROR:\n{compileErr}");
		_shader = _rd.ShaderCreateFromSpirV(spirv);
		if (_shader.IsValid)
		{
			_pipeline = _rd.ComputePipelineCreate(_shader);
			GD.Print($"[DustMotes] Shader + Pipeline OK (pipeline.valid={_pipeline.IsValid})");
		}
		else
		{
			GD.PrintErr("[DustMotes] Shader RID invalid — pipeline not created, dust pass will be a no-op");
		}
		_sampler = _rd.SamplerCreate(new RDSamplerState());

		_colorUniform = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = 0 };
		_depthUniform = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 1 };
		_uniformList = new Godot.Collections.Array<RDUniform> { _colorUniform, _depthUniform };
	}

	private Rid GetOrCreateSet(Rid color, Rid depth)
	{
		var key = (color.Id, depth.Id);
		if (_setCache.TryGetValue(key, out var cached))
		{
			if (_rd.UniformSetIsValid(cached))
				return cached;
			_setCache.Remove(key);
		}

		_colorUniform.ClearIds();
		_colorUniform.AddId(color);
		_depthUniform.ClearIds();
		_depthUniform.AddId(_sampler);
		_depthUniform.AddId(depth);

		Rid fresh = _rd.UniformSetCreate(_uniformList, _shader, 0);
		_setCache[key] = fresh;
		return fresh;
	}

	private void RunPass(Rid set, Projection invViewProj, Vector3 camPos, Vector2I size,
		uint xGroups, uint yGroups, float time)
	{
		_pushFloats[0] = invViewProj.X.X;
		_pushFloats[1] = invViewProj.X.Y;
		_pushFloats[2] = invViewProj.X.Z;
		_pushFloats[3] = invViewProj.X.W;
		_pushFloats[4] = invViewProj.Y.X;
		_pushFloats[5] = invViewProj.Y.Y;
		_pushFloats[6] = invViewProj.Y.Z;
		_pushFloats[7] = invViewProj.Y.W;
		_pushFloats[8] = invViewProj.Z.X;
		_pushFloats[9] = invViewProj.Z.Y;
		_pushFloats[10] = invViewProj.Z.Z;
		_pushFloats[11] = invViewProj.Z.W;
		_pushFloats[12] = invViewProj.W.X;
		_pushFloats[13] = invViewProj.W.Y;
		_pushFloats[14] = invViewProj.W.Z;
		_pushFloats[15] = invViewProj.W.W;
		_pushFloats[16] = camPos.X;
		_pushFloats[17] = camPos.Y;
		_pushFloats[18] = camPos.Z;
		_pushFloats[19] = time;
		_pushFloats[20] = size.X;
		_pushFloats[21] = size.Y;
		_pushFloats[22] = Brightness;
		_pushFloats[23] = Density;
		_pushFloats[24] = Sparkle;
		_pushFloats[25] = TwinkleSpeed;
		_pushFloats[26] = NearFade;
		_pushFloats[27] = FarFade;
		_pushFloats[28] = CellSize;
		_pushFloats[29] = MoteRadius;
		_pushFloats[30] = StartDist;
		_pushFloats[31] = Coverage;
		System.Buffer.BlockCopy(_pushFloats, 0, _pushBytes, 0, _pushBytes.Length);

		long list = _rd.ComputeListBegin();
		_rd.ComputeListBindComputePipeline(list, _pipeline);
		_rd.ComputeListBindUniformSet(list, set, 0);
		_rd.ComputeListSetPushConstant(list, _pushBytes, (uint)_pushBytes.Length);
		_rd.ComputeListDispatch(list, xGroups, yGroups, 1);
		_rd.ComputeListEnd();
	}

	public DustMotesEffect()
	{
		EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
		AccessResolvedColor = true;
		AccessResolvedDepth = true;
		if (!IsHeadless())
		{
			GD.Print("[DustMotes] ctor — queueing InitializeCompute on render thread");
			RenderingServer.CallOnRenderThread(Callable.From(InitializeCompute));
		}
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
		}
	}

	public override void _RenderCallback(int effectCallbackType, RenderData renderData)
	{
		if (_rd == null || !_pipeline.IsValid)
		{
			if (System.Threading.Interlocked.Exchange(ref _firstRenderLogged, 1) == 0)
				GD.PrintErr($"[DustMotes] _RenderCallback early-return: rd={(_rd != null)} pipeline.valid={_pipeline.IsValid} — dust pass is silently OFF this run");
			return;
		}
		if (!AnyEffectActive)
			return;
		if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers)
			return;
		if (renderData.GetRenderSceneData() is not RenderSceneDataRD sceneData)
			return;
		if (System.Threading.Interlocked.Exchange(ref _firstRenderLogged, 1) == 0)
			GD.Print("[DustMotes] _RenderCallback first dispatch — dust pass IS running");

		Vector2I size = buffers.GetInternalSize();
		if (size.X == 0 || size.Y == 0)
			return;

		uint xGroups = ((uint)size.X + 15) / 16;
		uint yGroups = ((uint)size.Y + 15) / 16;
		float time = (Time.GetTicksMsec() % 100000UL) / 1000.0f;
		uint views = buffers.GetViewCount();
		Vector3 camPos = sceneData.GetCamTransform().Origin;

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

			Rid depth = buffers.GetDepthLayer(view);
			Rid set = GetOrCreateSet(color, depth);
			if (!set.IsValid)
				return;

			Projection viewMatrix = new Projection(sceneData.GetCamTransform().AffineInverse());
			Projection invViewProj = (sceneData.GetViewProjection(view) * viewMatrix).Inverse();
			RunPass(set, invViewProj, camPos, size, xGroups, yGroups, time);
		}
	}
}
