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

using System;
using System.Collections.Generic;
using Godot;

namespace Vantix.Config;

/// <summary>Settings menu, opened via ToggleKey (ESC by default). Code-driven UI; live-applies on change, with
/// an explicit Save. A preset sets every value; tweaking one switches the preset to Custom.</summary>
public partial class SettingsMenu : CanvasLayer
{
	private Control _root;
	private bool _isOpen;
	private Input.MouseModeEnum _mouseModeBeforeOpen = Input.MouseModeEnum.Visible;
	private Button _disconnectBtn;

	private OptionButton _presetOpt;
	private HSlider _renderScaleSlider;
	private Label _renderScaleValue;
	private HSlider _viewmodelScaleSlider;
	private Label _viewmodelScaleValue;
	private HSlider _uiScaleSlider;
	private Label _uiScaleValue;
	private OptionButton _uiMsaaOpt;
	private OptionButton _aaOpt;
	private OptionButton _upscalerOpt;
	private OptionButton _shadowsOpt;
	private OptionButton _anisotropyOpt;
	private OptionButton _aoOpt;
	private OptionButton _reflectionsOpt;
	private OptionButton _reflectionProbesOpt;
	private OptionButton _volumetricFogOpt;
	private OptionButton _skyOpt;
	private OptionButton _cloudShadowsOpt;
	private OptionButton _postProcessingOpt;
	private OptionButton _lensFlareOpt;
	private OptionButton _bloomOpt;
	private OptionButton _dustMotesOpt;
	private OptionButton _motionBlurOpt;
	private OptionButton _filmGrainOpt;
	private OptionButton _vignetteOpt;
	private OptionButton _sharpeningOpt;
	private OptionButton _chromaticAberrationOpt;
	private OptionButton _adsDofOpt;
	private OptionButton _adsFovZoomOpt;
	private OptionButton _autoExposureOpt;
	private OptionButton _eyeAdaptationOpt;
	private OptionButton _purkinjeOpt;
	private OptionButton _cinematicBandsOpt;
	private OptionButton _teamGlowOpt;
	private OptionButton _viewBobOpt;
	private OptionButton _sprintSwayOpt;
	private OptionButton _mouseInertiaOpt;
	private OptionButton _directionLeanOpt;
	private OptionButton _cameraShakeOpt;
	private OptionButton _showDebugBarOpt;
	private OptionButton _showNetGraphOpt;
	private OptionButton _weaponLightOpt;
	private OptionButton _windowModeOpt;
	private OptionButton _monitorOpt;
	private OptionButton _resolutionOpt;
	private OptionButton _vsyncOpt;
	private OptionButton _fpsCapOpt;
	private OptionButton _menuFpsCapOpt;
	private HSlider _brightnessSlider;
	private Label _brightnessValue;
	private HSlider _hudMarginHSlider;
	private Label _hudMarginHValue;
	private HSlider _hudMarginVSlider;
	private Label _hudMarginVValue;
	private HSlider _sensSlider;
	private Label _sensValue;
	private HSlider _fovSlider;
	private Label _fovValue;
	private Label _saveStatus;

	private bool _suppressEvents;
	private Vector2I[] _resolutions;
	private int[] _fpsCaps;

	private static readonly int[] MenuCaps = { 30, 60, 90, 120, 144 };
	private static readonly string[] OnOff = { "Off", "On" };

	public static bool IsAnyOpen { get; private set; }

	[Export]
	public Key ToggleKey = Key.Escape;

	[Export]
	public int LayerOrder = 200;

	private void SetOpen(bool open)
	{
		_isOpen = open;
		IsAnyOpen = open;
		_root.Visible = open;
		if (open)
		{
			_mouseModeBeforeOpen = Input.MouseMode;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			Engine.MaxFps = Settings.MenuFpsCap;
			if (_disconnectBtn != null)
				_disconnectBtn.Visible = NetMain.Instance?.Client?.Connected == true;
		}
		else
		{
			Input.MouseMode = _mouseModeBeforeOpen;
			Engine.MaxFps = Settings.FpsCap;
		}
	}

	private void BuildUI()
	{
		_root = new Control
		{
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		AddChild(_root);

		var bg = new ColorRect
		{
			AnchorLeft = 0f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			Color = new Color(0f, 0f, 0f, 0f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_root.AddChild(bg);

		var panel = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -280f,
			OffsetRight = 280f,
			OffsetTop = -330f,
			OffsetBottom = 330f,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.1f, 0.12f, 0.95f),
			BorderColor = new Color(0.4f, 0.7f, 0.4f, 0.8f),
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
		};
		style.ContentMarginLeft = 24f;
		style.ContentMarginRight = 24f;
		style.ContentMarginTop = 18f;
		style.ContentMarginBottom = 18f;
		panel.AddThemeStyleboxOverride("panel", style);
		_root.AddChild(panel);

		var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		outer.AddThemeConstantOverride("separation", 10);
		panel.AddChild(outer);

		var title = new Label { Text = "SETTINGS  (ESC to close)" };
		title.AddThemeFontSizeOverride("font_size", 18);
		title.AddThemeColorOverride("font_color", new Color(0.85f, 1f, 0.85f));
		outer.AddChild(title);

		var tabs = new TabContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		outer.AddChild(tabs);
		tabs.AddChild(BuildGraphicsTab());
		tabs.AddChild(BuildDisplayTab());
		tabs.AddChild(BuildControlsTab());

		outer.AddChild(new HSeparator());
		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 12);
		outer.AddChild(btnRow);

		var saveBtn = new Button { Text = "  Save  ", CustomMinimumSize = new Vector2(120, 36) };
		saveBtn.Pressed += OnSavePressed;
		btnRow.AddChild(saveBtn);

		var closeBtn = new Button { Text = "  Close  ", CustomMinimumSize = new Vector2(120, 36) };
		closeBtn.Pressed += () => SetOpen(false);
		btnRow.AddChild(closeBtn);

		_saveStatus = new Label { Text = "" };
		_saveStatus.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.7f));
		_saveStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		btnRow.AddChild(_saveStatus);

		// Visibility refreshed in SetOpen; shown only when connected.
		_disconnectBtn = new Button { Text = "  Disconnect  ", CustomMinimumSize = new Vector2(140, 36) };
		_disconnectBtn.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.5f));
		_disconnectBtn.Pressed += OnDisconnectPressed;
		btnRow.AddChild(_disconnectBtn);

		var quitBtn = new Button { Text = "  Quit  ", CustomMinimumSize = new Vector2(120, 36) };
		quitBtn.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.55f));
		quitBtn.Pressed += () => GetTree().Quit();
		btnRow.AddChild(quitBtn);
	}

	private void OnDisconnectPressed()
	{
		SetOpen(false);
		NetMain.Instance?.RequestDisconnect("Disconnected by user");
	}

	private static (ScrollContainer page, VBoxContainer vbox) NewTabPage(string name)
	{
		var scroll = new ScrollContainer
		{
			Name = name,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		vbox.AddThemeConstantOverride("separation", 9);
		scroll.AddChild(vbox);
		return (scroll, vbox);
	}

	private Control BuildGraphicsTab()
	{
		var (page, vbox) = NewTabPage("Graphics");

		AddSectionHeader(vbox, "QUALITY");
		_presetOpt = AddDropdown(vbox, "Quality-Preset", new[] { "Low", "Medium", "High", "Ultra", "Custom" });
		_presetOpt.ItemSelected += OnPresetChanged;

		// >100% supersamples the weapon viewport (SSAA) — clean iron-sight edges, no temporal ghosting.
		(_renderScaleSlider, _renderScaleValue) = AddFloatSlider(vbox, "Render Scale", 0.5f, 2.0f, 0.05f,
			v => Settings.RenderScale = v, Percent, markCustom: true);
		(_viewmodelScaleSlider, _viewmodelScaleValue) = AddFloatSlider(vbox, "Viewmodel Render Scale", 0.5f, 2.0f, 0.05f,
			v => Settings.ViewmodelRenderScale = v, Percent, markCustom: true);

		_uiMsaaOpt = AddIntOption(vbox, "UI Quality (MSAA 2D)", new[] { "Off", "2×", "4×", "8×" },
			i => Settings.UiMsaa = (Viewport.Msaa)i);
		_aaOpt = AddIntOption(vbox, "Anti-Aliasing", new[] { "Off", "FXAA", "SMAA", "TAA" },
			i => Settings.AntiAliasing = (AntiAliasingMode)i);
		_upscalerOpt = AddIntOption(vbox, "Upscaler", new[] { "Bilinear", "FSR 1.0", "FSR 2.0" },
			i => Settings.Upscaler = (UpscalingMode)i);
		_shadowsOpt = AddIntOption(vbox, "Shadows", new[] { "Off", "Low", "Medium", "High" },
			i => Settings.Shadows = (ShadowQuality)i);
		_anisotropyOpt = AddIntOption(vbox, "Anisotropic Filtering", new[] { "Off", "2×", "4×", "8×", "16×" },
			i => Settings.Anisotropy = (AnisotropicFiltering)i);

		AddSectionHeader(vbox, "WORLD EFFECTS");
		_aoOpt = AddBoolOption(vbox, "Ambient Occlusion", on => Settings.AmbientOcclusion = on);
		_reflectionsOpt = AddBoolOption(vbox, "Reflections", on => Settings.Reflections = on);
		// Atlas quality (per-probe pixel size); change needs a level reload.
		_reflectionProbesOpt = AddIntOption(vbox, "Reflection Probes",
			new[] { "Low (128)", "Medium (256)", "High (512)", "Ultra (1024)" },
			i => Settings.ReflectionProbes = (ReflectionProbeQuality)i);
		// Off = perf-diagnosis; smoke-grenade FogVolumes render nothing. Indices 0..3 map 1:1 to the enum.
		_volumetricFogOpt = AddIntOption(vbox, "Volumetric Fog",
			new[] { "Off (smokes invisible)", "Low (64³)", "Medium (96³)", "High (160³)" },
			i => Settings.VolumetricFog = (VolumetricFogQuality)i);
		_skyOpt = AddBoolOption(vbox, "Sky", on => Settings.Sky = on);
		_cloudShadowsOpt = AddBoolOption(vbox, "Cloud Shadows", on => Settings.CloudShadows = on);
		// Master toggle for the post-process compositor; Off skips the whole dispatch.
		_postProcessingOpt = AddBoolOption(vbox, "Post Processing", on => Settings.PostProcessing = on);
		_lensFlareOpt = AddBoolOption(vbox, "Lens Flare", on => Settings.LensFlare = on);
		_bloomOpt = AddBoolOption(vbox, "Bloom", on => Settings.Bloom = on);
		_dustMotesOpt = AddBoolOption(vbox, "Dust Motes", on => Settings.DustMotes = on);

		AddSectionHeader(vbox, "POST-PROCESSING");
		_motionBlurOpt = AddBoolOption(vbox, "Motion Blur", on => Settings.MotionBlur = on);
		_filmGrainOpt = AddBoolOption(vbox, "Film Grain", on => Settings.FilmGrain = on);
		_vignetteOpt = AddBoolOption(vbox, "Vignette", on => Settings.Vignette = on);
		_chromaticAberrationOpt = AddBoolOption(vbox, "Chromatic Aberration", on => Settings.ChromaticAberration = on);
		_sharpeningOpt = AddBoolOption(vbox, "Sharpening", on => Settings.Sharpening = on);
		_adsDofOpt = AddBoolOption(vbox, "Depth of Field (ADS)", on => Settings.AdsDepthOfField = on);
		_adsFovZoomOpt = AddBoolOption(vbox, "ADS FOV Zoom", on => Settings.AdsFovZoom = on);
		_autoExposureOpt = AddBoolOption(vbox, "Auto Exposure", on => Settings.AutoExposure = on);
		_eyeAdaptationOpt = AddBoolOption(vbox, "Eye Adaptation", on => Settings.EyeAdaptation = on);
		_purkinjeOpt = AddBoolOption(vbox, "Purkinje", on => Settings.Purkinje = on);
		_cinematicBandsOpt = AddBoolOption(vbox, "Cinematic Bands", on => Settings.CinematicBands = on);
		// PuppetPlayer reads TeamGlow per update; no Apply needed.
		_teamGlowOpt = AddBoolOption(vbox, "Team Glow (Outline)", on => Settings.TeamGlow = on, apply: false);

		AddSectionHeader(vbox, "CAMERA EFFECTS");
		_viewBobOpt = AddBoolOption(vbox, "View Bob (Walk Bob)", on => Settings.ViewBob = on, apply: false);
		_sprintSwayOpt = AddBoolOption(vbox, "Sprint Sway", on => Settings.SprintSway = on, apply: false);
		_mouseInertiaOpt = AddBoolOption(vbox, "Mouse Inertia (Weapon-Lag)", on => Settings.MouseInertia = on, apply: false);
		_directionLeanOpt = AddBoolOption(vbox, "Direction Lean (Strafe-Tilt)", on => Settings.DirectionLean = on, apply: false);
		_cameraShakeOpt = AddBoolOption(vbox, "Camera Shake (Firing)", on => Settings.CameraShake = on, apply: false);

		AddSectionHeader(vbox, "DEBUG OVERLAYS");
		_showDebugBarOpt = AddBoolOption(vbox, "Debug Bar (F3)", on => Settings.ShowDebugBar = on, markCustom: false, apply: false);
		_showNetGraphOpt = AddBoolOption(vbox, "Net Graph (F4)", on => Settings.ShowNetGraph = on, markCustom: false, apply: false);
		_weaponLightOpt = AddBoolOption(vbox, "Weapon Light (debug)", on => Settings.WeaponLight = on, markCustom: false, apply: false);

		return page;
	}

	private Control BuildDisplayTab()
	{
		var (page, vbox) = NewTabPage("Display");

		_windowModeOpt = AddDropdown(vbox, "Window Mode",
			new[] { "Windowed", "Borderless Fullscreen", "Exclusive Fullscreen" });
		_windowModeOpt.ItemSelected += OnWindowModeChanged;

		_monitorOpt = AddDropdown(vbox, "Monitor", BuildMonitorLabels());
		_monitorOpt.ItemSelected += OnMonitorChanged;

		_resolutionOpt = AddDropdown(vbox, "Resolution", Array.Empty<string>());
		RebuildResolutionDropdown();
		_resolutionOpt.ItemSelected += OnResolutionChanged;

		_vsyncOpt = AddDropdown(vbox, "VSync", new[] { "Disabled", "Enabled", "Adaptive", "Mailbox" });
		_vsyncOpt.ItemSelected += OnVSyncChanged;

		string[] fpsStrings = new string[_fpsCaps.Length];
		for (int i = 0; i < _fpsCaps.Length; i++)
			fpsStrings[i] = _fpsCaps[i] == 0 ? "Unlimited" : _fpsCaps[i].ToString();
		_fpsCapOpt = AddDropdown(vbox, "FPS Cap", fpsStrings);
		_fpsCapOpt.ItemSelected += OnFpsCapChanged;

		string[] menuCapStr = new string[MenuCaps.Length];
		for (int i = 0; i < MenuCaps.Length; i++)
			menuCapStr[i] = MenuCaps[i].ToString();
		_menuFpsCapOpt = AddDropdown(vbox, "Menu FPS Cap", menuCapStr);
		_menuFpsCapOpt.ItemSelected += OnMenuFpsCapChanged;

		(_brightnessSlider, _brightnessValue) = AddFloatSlider(vbox, "Brightness", 0.6f, 1.4f, 0.05f,
			v => Settings.Brightness = v, Percent);
		(_uiScaleSlider, _uiScaleValue) = AddFloatSlider(vbox, "UI Scale", 0.8f, 1.5f, 0.05f,
			v => Settings.UiScale = v, Percent);

		AddSectionHeader(vbox, "HUD");
		// HudCs2 reads the margins per frame → no Apply needed.
		(_hudMarginHSlider, _hudMarginHValue) = AddFloatSlider(vbox, "HUD Margin Horizontal", 0f, 140f, 2f,
			v => Settings.HudMarginH = v, Px, apply: false);
		(_hudMarginVSlider, _hudMarginVValue) = AddFloatSlider(vbox, "HUD Margin Vertical", 0f, 140f, 2f,
			v => Settings.HudMarginV = v, Px, apply: false);

		return page;
	}

	private Control BuildControlsTab()
	{
		var (page, vbox) = NewTabPage("Controls");

		(_sensSlider, _sensValue) = AddFloatSlider(vbox, "Mouse Sens", 0.01f, 10.0f, 0.01f,
			v => Settings.MouseSensitivity = v, Decimal2);
		(_fovSlider, _fovValue) = AddFloatSlider(vbox, "FOV", 80f, 100f, 1f,
			v => Settings.Fov = v, Degrees);

		return page;
	}

	// Resolutions come from native enumeration (Win32/xrandr) for the exact advertised modes; falls back to a
	// hardcoded candidate list filtered by native size when no native backend exists (macOS/Wayland).
	private void BuildDynamicLists()
	{
		int screenCount = DisplayServer.GetScreenCount();
		int idx = Settings.MonitorIndex;
		if (idx < 0 || idx >= screenCount) idx = 0;

		Vector2I[] enumerated = Win32Display.IsSupported
			? Win32Display.EnumModes(idx)
			: LinuxDisplay.IsSupported ? LinuxDisplay.EnumModes(idx) : Array.Empty<Vector2I>();

		if (enumerated.Length > 0)
		{
			_resolutions = enumerated;
		}
		else
		{
			Vector2I native = DisplayServer.ScreenGetSize(idx);
			int maxW = native.X, maxH = native.Y;
			var candidates = new List<Vector2I>
			{
				new(640, 480),    new(800, 600),    new(1024, 768),   new(1280, 1024),  // 4:3
				new(1280, 720),   new(1366, 768),   new(1600, 900),   new(1920, 1080),  // 16:9
				new(2560, 1440),  new(3840, 2160),
				new(1280, 800),   new(1440, 900),   new(1680, 1050),  new(1920, 1200),  // 16:10
				new(2560, 1600),
				new(2560, 1080),  new(3440, 1440),  new(3840, 1600),  new(5120, 2160),  // 21:9 / 32:9
			};
			var resList = new List<Vector2I>();
			foreach (var r in candidates)
				if (r.X <= maxW && r.Y <= maxH)
					resList.Add(r);
			if (!resList.Contains(native)) resList.Add(native);
			resList.Sort((a, b) => (a.X * a.Y).CompareTo(b.X * b.Y));
			_resolutions = resList.ToArray();
		}

		int hz = (int)Mathf.Round(DisplayServer.ScreenGetRefreshRate(idx));
		if (hz <= 0) hz = 60;
		var capsSet = new HashSet<int> { 30, 60, hz, hz * 2 };
		var capsList = new List<int>(capsSet);
		capsList.Sort();
		capsList.Insert(0, 0);
		_fpsCaps = capsList.ToArray();
	}

	// "Display N" + native res + refresh + primary tag. Uses Win32/xrandr for physical pixels and primary
	// detection — Godot's APIs mis-report under per-monitor DPI.
	private static string[] BuildMonitorLabels()
	{
		int n = DisplayServer.GetScreenCount();
		int godotPrimary = DisplayServer.GetPrimaryScreen();
		string[] labels = new string[n];
		for (int i = 0; i < n; i++)
		{
			Vector2I size = Win32Display.IsSupported
				? Win32Display.GetNativeResolution(i)
				: LinuxDisplay.IsSupported ? LinuxDisplay.GetNativeResolution(i) : Vector2I.Zero;
			if (size == Vector2I.Zero) size = DisplayServer.ScreenGetSize(i);
			int hz = (int)Mathf.Round(DisplayServer.ScreenGetRefreshRate(i));
			int displayNum = Win32Display.IsSupported ? Win32Display.GetWindowsDisplayNumber(i) : i + 1;
			bool isPrimary = Win32Display.IsSupported ? Win32Display.IsPrimaryMonitor(i) : i == godotPrimary;
			labels[i] = $"Display {displayNum} — {size.X}×{size.Y}@{hz}Hz" + (isPrimary ? " (primary)" : "");
		}
		return labels;
	}

	private void AddSectionHeader(VBoxContainer parent, string text)
	{
		parent.AddChild(new HSeparator());
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.55f));
		parent.AddChild(lbl);
	}

	private OptionButton AddDropdown(VBoxContainer parent, string label, string[] items)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		parent.AddChild(row);

		row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(170, 0) });

		var opt = new OptionButton { CustomMinimumSize = new Vector2(250, 0) };
		foreach (var it in items)
			opt.AddItem(it);
		row.AddChild(opt);
		return opt;
	}

	private (HSlider, Label) AddSlider(VBoxContainer parent, string label, float min, float max, float step)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		parent.AddChild(row);

		row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(170, 0) });

		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			CustomMinimumSize = new Vector2(190, 0),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		row.AddChild(slider);

		var val = new Label { Text = "", CustomMinimumSize = new Vector2(54, 0) };
		row.AddChild(val);
		return (slider, val);
	}

	private OptionButton AddIntOption(VBoxContainer parent, string label, string[] items, Action<int> set,
		bool markCustom = true, bool apply = true)
	{
		var opt = AddDropdown(parent, label, items);
		opt.ItemSelected += idx =>
		{
			if (_suppressEvents)
				return;
			set((int)idx);
			if (markCustom)
				MarkCustomPreset();
			if (apply)
				Settings.Apply(GetTree());
		};
		return opt;
	}

	private OptionButton AddBoolOption(VBoxContainer parent, string label, Action<bool> set,
		bool markCustom = true, bool apply = true)
		=> AddIntOption(parent, label, OnOff, i => set(i == 1), markCustom, apply);

	private (HSlider, Label) AddFloatSlider(VBoxContainer parent, string label, float min, float max, float step,
		Action<float> set, Func<double, string> format, bool markCustom = false, bool apply = true)
	{
		var (slider, val) = AddSlider(parent, label, min, max, step);
		slider.ValueChanged += v =>
		{
			val.Text = format(v);
			if (_suppressEvents)
				return;
			set((float)v);
			if (markCustom)
				MarkCustomPreset();
			if (apply)
				Settings.Apply(GetTree());
		};
		return (slider, val);
	}

	private static void SeedSlider(HSlider slider, Label val, double value, Func<double, string> format)
	{
		slider.Value = value;
		val.Text = format(value);
	}

	private static string Percent(double v) => Mathf.RoundToInt((float)v * 100f) + "%";

	private static string Px(double v) => Mathf.RoundToInt((float)v) + " px";

	private static string Decimal2(double v) => v.ToString("F2");

	private static string Degrees(double v) => v.ToString("F0") + "°";

	private void RebuildResolutionDropdown()
	{
		_resolutionOpt.Clear();
		foreach (var r in _resolutions)
			_resolutionOpt.AddItem(FormatResolutionLabel(r));
	}

	private void PullStateFromSettings()
	{
		_suppressEvents = true;

		_windowModeOpt.Selected = WindowModeToIndex(Settings.WindowMode);
		// Sync MonitorIndex with where the window actually is now; rebuild the list if the active monitor changed.
		int actualMonitor = DisplayServer.WindowGetCurrentScreen();
		if (actualMonitor != Settings.MonitorIndex)
		{
			Settings.MonitorIndex = actualMonitor;
			BuildDynamicLists();
			RebuildResolutionDropdown();
		}
		_monitorOpt.Selected = Mathf.Clamp(Settings.MonitorIndex, 0, _monitorOpt.ItemCount - 1);
		_resolutionOpt.Selected = ResolutionToIndex(Settings.Resolution);
		_vsyncOpt.Selected = (int)Settings.VSync;
		_fpsCapOpt.Selected = IndexOf(_fpsCaps, Settings.FpsCap, 0);
		_menuFpsCapOpt.Selected = IndexOf(MenuCaps, Settings.MenuFpsCap, 1);

		SeedSlider(_sensSlider, _sensValue, Settings.MouseSensitivity, Decimal2);
		SeedSlider(_fovSlider, _fovValue, Settings.Fov, Degrees);
		SeedSlider(_brightnessSlider, _brightnessValue, Settings.Brightness, Percent);
		SeedSlider(_hudMarginHSlider, _hudMarginHValue, Settings.HudMarginH, Px);
		SeedSlider(_hudMarginVSlider, _hudMarginVValue, Settings.HudMarginV, Px);

		RefreshGraphicsControls();

		_suppressEvents = false;
	}

	private void RefreshGraphicsControls()
	{
		bool prev = _suppressEvents;
		_suppressEvents = true;

		_presetOpt.Selected = (int)Settings.Preset;
		SeedSlider(_renderScaleSlider, _renderScaleValue, Settings.RenderScale, Percent);
		SeedSlider(_viewmodelScaleSlider, _viewmodelScaleValue, Settings.ViewmodelRenderScale, Percent);
		SeedSlider(_uiScaleSlider, _uiScaleValue, Settings.UiScale, Percent);
		_uiMsaaOpt.Selected = (int)Settings.UiMsaa;
		_aaOpt.Selected = (int)Settings.AntiAliasing;
		_upscalerOpt.Selected = (int)Settings.Upscaler;
		_shadowsOpt.Selected = (int)Settings.Shadows;
		_anisotropyOpt.Selected = (int)Settings.Anisotropy;
		_aoOpt.Selected = Settings.AmbientOcclusion ? 1 : 0;
		_reflectionsOpt.Selected = Settings.Reflections ? 1 : 0;
		_reflectionProbesOpt.Selected = (int)Settings.ReflectionProbes;
		_volumetricFogOpt.Selected = (int)Settings.VolumetricFog;
		_skyOpt.Selected = Settings.Sky ? 1 : 0;
		_cloudShadowsOpt.Selected = Settings.CloudShadows ? 1 : 0;
		_postProcessingOpt.Selected = Settings.PostProcessing ? 1 : 0;
		_lensFlareOpt.Selected = Settings.LensFlare ? 1 : 0;
		_bloomOpt.Selected = Settings.Bloom ? 1 : 0;
		_dustMotesOpt.Selected = Settings.DustMotes ? 1 : 0;
		_motionBlurOpt.Selected = Settings.MotionBlur ? 1 : 0;
		_filmGrainOpt.Selected = Settings.FilmGrain ? 1 : 0;
		_vignetteOpt.Selected = Settings.Vignette ? 1 : 0;
		_sharpeningOpt.Selected = Settings.Sharpening ? 1 : 0;
		_chromaticAberrationOpt.Selected = Settings.ChromaticAberration ? 1 : 0;
		_adsDofOpt.Selected = Settings.AdsDepthOfField ? 1 : 0;
		_adsFovZoomOpt.Selected = Settings.AdsFovZoom ? 1 : 0;
		_autoExposureOpt.Selected = Settings.AutoExposure ? 1 : 0;
		_eyeAdaptationOpt.Selected = Settings.EyeAdaptation ? 1 : 0;
		_purkinjeOpt.Selected = Settings.Purkinje ? 1 : 0;
		_cinematicBandsOpt.Selected = Settings.CinematicBands ? 1 : 0;
		_teamGlowOpt.Selected = Settings.TeamGlow ? 1 : 0;
		_viewBobOpt.Selected = Settings.ViewBob ? 1 : 0;
		_sprintSwayOpt.Selected = Settings.SprintSway ? 1 : 0;
		_mouseInertiaOpt.Selected = Settings.MouseInertia ? 1 : 0;
		_directionLeanOpt.Selected = Settings.DirectionLean ? 1 : 0;
		_cameraShakeOpt.Selected = Settings.CameraShake ? 1 : 0;
		_showDebugBarOpt.Selected = Settings.ShowDebugBar ? 1 : 0;
		_showNetGraphOpt.Selected = Settings.ShowNetGraph ? 1 : 0;
		_weaponLightOpt.Selected = Settings.WeaponLight ? 1 : 0;

		_suppressEvents = prev;
	}

	private void MarkCustomPreset()
	{
		Settings.Preset = QualityPreset.Custom;
		_presetOpt.Selected = (int)QualityPreset.Custom;
	}

	private void OnPresetChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.ApplyPreset((QualityPreset)(int)idx);
		RefreshGraphicsControls();
		Settings.Apply(GetTree());
	}

	private void OnWindowModeChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.WindowMode = IndexToWindowMode((int)idx);
		Settings.Apply(GetTree());
	}

	private void OnResolutionChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.Resolution = _resolutions[(int)idx];
		Settings.Apply(GetTree());
	}

	private void OnMonitorChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.MonitorIndex = (int)idx;
		BuildDynamicLists();
		_suppressEvents = true;
		RebuildResolutionDropdown();
		// Closest match to the previous Resolution, falling back to native.
		int newIdx = ResolutionToIndex(Settings.Resolution);
		Settings.Resolution = _resolutions[newIdx];
		_resolutionOpt.Selected = newIdx;
		_suppressEvents = false;
		Settings.Apply(GetTree());
	}

	private void OnVSyncChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.VSync = (DisplayServer.VSyncMode)(int)idx;
		Settings.Apply(GetTree());
	}

	private void OnFpsCapChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.FpsCap = _fpsCaps[(int)idx];
		// Deferred while the menu is open; MenuFpsCap stays in effect until close.
		if (!_isOpen)
			Settings.Apply(GetTree());
	}

	private void OnMenuFpsCapChanged(long idx)
	{
		if (_suppressEvents)
			return;
		Settings.MenuFpsCap = MenuCaps[(int)idx];
		if (_isOpen)
			Engine.MaxFps = Settings.MenuFpsCap;
	}

	private void OnSavePressed()
	{
		Settings.Save();
		_saveStatus.Text = "✓ Saved";
		GetTree().CreateTimer(2.0).Timeout += () =>
		{
			if (_saveStatus != null)
				_saveStatus.Text = "";
		};
	}

	private static int WindowModeToIndex(DisplayServer.WindowMode m) =>
		m switch
		{
			DisplayServer.WindowMode.Windowed => 0,
			DisplayServer.WindowMode.Fullscreen => 1,
			DisplayServer.WindowMode.ExclusiveFullscreen => 2,
			_ => 0,
		};

	private static DisplayServer.WindowMode IndexToWindowMode(int i) =>
		i switch
		{
			0 => DisplayServer.WindowMode.Windowed,
			1 => DisplayServer.WindowMode.Fullscreen,
			2 => DisplayServer.WindowMode.ExclusiveFullscreen,
			_ => DisplayServer.WindowMode.Windowed,
		};

	private int ResolutionToIndex(Vector2I res)
	{
		for (int i = 0; i < _resolutions.Length; i++)
			if (_resolutions[i] == res)
				return i;
		return _resolutions.Length - 1;
	}

	private static int IndexOf(int[] arr, int value, int fallback)
	{
		for (int i = 0; i < arr.Length; i++)
			if (arr[i] == value)
				return i;
		return fallback;
	}

	private static string FormatResolutionLabel(Vector2I r)
	{
		string aspect = AspectRatioTag(r);
		return aspect != null ? $"{r.X}×{r.Y} ({aspect})" : $"{r.X}×{r.Y}";
	}

	private static string AspectRatioTag(Vector2I r)
	{
		if (r.Y == 0) return null;
		int g = Gcd(r.X, r.Y);
		int ax = r.X / g, ay = r.Y / g;
		// Snap to canonical ratios so e.g. 1366×768 still reads "16:9".
		float ratio = (float)r.X / r.Y;
		if (Mathf.Abs(ratio - 16f / 9f) < 0.02f) return "16:9";
		if (Mathf.Abs(ratio - 16f / 10f) < 0.02f) return "16:10";
		if (Mathf.Abs(ratio - 4f / 3f) < 0.02f) return "4:3";
		if (Mathf.Abs(ratio - 5f / 4f) < 0.02f) return "5:4";
		if (Mathf.Abs(ratio - 21f / 9f) < 0.03f) return "21:9";
		if (Mathf.Abs(ratio - 32f / 9f) < 0.03f) return "32:9";
		if (ax <= 32 && ay <= 32) return $"{ax}:{ay}";
		return null;
	}

	private static int Gcd(int a, int b) { while (b != 0) { int t = b; b = a % b; a = t; } return a; }

	public override void _Ready()
	{
		if (NetMain.Instance?.Cli?.Mode == NetMode.Server) { QueueFree(); return; }
		Layer = LayerOrder;
		BuildDynamicLists();
		BuildUI();
		SetOpen(false);
		PullStateFromSettings();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == ToggleKey)
		{
			SetOpen(!_isOpen);
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>Opens the menu programmatically (used by the main menu Settings button).</summary>
	public void Open() => SetOpen(true);
}
