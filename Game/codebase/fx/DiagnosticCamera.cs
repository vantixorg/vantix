using Godot;

namespace Vantix.Fx;

/// <summary>
/// Fixed-position diagnostic camera to isolate the world-render pipeline from viewmodel and
/// WorldEnvironment, for bisecting a render artefact. While DiagEnabled, re-asserts Current=true
/// every _Process so the player camera can't reclaim focus.
/// BypassCompositor attaches an empty Compositor (no PostProcessEffect); BypassEnvironment attaches
/// an empty Environment (no tonemap/glow/SSR/SSIL/SSAO/LUT/fog). If the artefact survives both, it's in the world.
/// </summary>
[Tool]
public partial class DiagnosticCamera : Camera3D
{
	/// <summary>When true, this camera force-claims rendering every frame.</summary>
	[Export] public bool DiagEnabled = true;

	/// <summary>Hide the viewmodel container while diagnostic mode is on; restored when it flips off.</summary>
	[Export] public bool HideViewmodel = true;

	/// <summary>Empty compositor override (bypasses CA/Sharpen/Vignette/Grain/MotionBlur).</summary>
	[Export] public bool BypassCompositor = false;

	/// <summary>Empty environment override (bypasses tonemap/glow/SSR/SSIL/SSAO/adjustment/fog/LUT).</summary>
	[Export] public bool BypassEnvironment = false;

	private SubViewportContainer _viewmodelContainer;
	private bool _viewmodelOriginalVisible = true;
	private bool _viewmodelCached;

	private Compositor _emptyCompositor;
	private Environment _emptyEnvironment;

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;

		if (DiagEnabled)
		{
			if (!Current) Current = true;
			if (HideViewmodel) ApplyHideViewmodel();
			ApplyCompositorBypass();
			ApplyEnvironmentBypass();
		}
		else
		{
			if (_viewmodelCached) RestoreViewmodel();
			if (Compositor != null) Compositor = null;
			if (Environment != null) Environment = null;
		}
	}

	/// <summary>Toggles the override compositor per BypassCompositor, reusing one empty instance.</summary>
	private void ApplyCompositorBypass()
	{
		if (BypassCompositor)
		{
			_emptyCompositor ??= new Compositor();
			if (Compositor != _emptyCompositor) Compositor = _emptyCompositor;
		}
		else
		{
			if (Compositor != null) Compositor = null;
		}
	}

	/// <summary>Toggles the override environment per BypassEnvironment, reusing one empty instance (engine defaults).</summary>
	private void ApplyEnvironmentBypass()
	{
		if (BypassEnvironment)
		{
			_emptyEnvironment ??= new Environment();
			if (Environment != _emptyEnvironment) Environment = _emptyEnvironment;
		}
		else
		{
			if (Environment != null) Environment = null;
		}
	}

	/// <summary>Finds the viewmodel container, caches its original visibility, hides it. Re-runs each frame to catch respawns.</summary>
	private void ApplyHideViewmodel()
	{
		if (_viewmodelContainer == null || !GodotObject.IsInstanceValid(_viewmodelContainer))
		{
			_viewmodelContainer = FindViewmodelContainer();
			_viewmodelCached = false;
			if (_viewmodelContainer == null) return;
		}
		if (!_viewmodelCached)
		{
			_viewmodelOriginalVisible = _viewmodelContainer.Visible;
			_viewmodelCached = true;
		}
		if (_viewmodelContainer.Visible) _viewmodelContainer.Visible = false;
	}

	/// <summary>Restores the viewmodel container's original visibility when DiagEnabled flips off.</summary>
	private void RestoreViewmodel()
	{
		if (_viewmodelContainer != null && GodotObject.IsInstanceValid(_viewmodelContainer))
			_viewmodelContainer.Visible = _viewmodelOriginalVisible;
		_viewmodelCached = false;
		_viewmodelContainer = null;
	}

	/// <summary>First SubViewportContainer named "viewmodel_container" in the tree.</summary>
	private SubViewportContainer FindViewmodelContainer()
	{
		foreach (Node n in GetTree().Root.FindChildren("viewmodel_container", "SubViewportContainer", true, false))
		{
			if (n is SubViewportContainer svc) return svc;
		}
		return null;
	}
}
