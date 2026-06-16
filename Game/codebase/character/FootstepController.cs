using Godot;

namespace Vantix.Character;

/// <summary>
/// Distance-based footstep cadence. Pure logic (no engine state), so server and
/// client replays match. Phase grows 1.0 per stride; integer crossings are steps,
/// and the same phase drives the view-bob so footsteps and bob stay in sync.
/// </summary>
public class FootstepController
{
	/// <summary>Tuning vars; swappable for tests.</summary>
	public SvConVars Sv = ConVars.Sv;

	private double _phase;
	private bool _leftFoot;
	private bool _wasMoving;

	/// <summary>True on the tick a step lands.</summary>
	public bool DidStepThisFrame { get; private set; }
	/// <summary>Step audibility 0..1, speed-scaled. Walking on shift emits nothing.</summary>
	public float StepLoudness { get; private set; }
	/// <summary>Alternates L/R per step so clients pick the same sample.</summary>
	public bool StepIsLeftFoot { get; private set; }

	/// <summary>Step phase wrapped to [0,2), used as the view-bob clock. +1.0 per step,
	/// integers are footplants, 2.0 is a full L+R cycle. Wrapping bounds precision.</summary>
	public float ContinuousPhase => (float)(_phase % 2.0);
	/// <summary>0..1 progress to the next step (debug overlay).</summary>
	public float StridePhase => (float)(_phase - Mathf.Floor((float)_phase));

	/// <summary>Reset on respawn/teleport so we don't emit a phantom step.</summary>
	public void Reset()
	{
		_phase = 0.0;
		_leftFoot = false;
		_wasMoving = false;
		DidStepThisFrame = false;
	}

	/// <summary>Advance the phase; emits a step on each integer crossing. Replay-safe.</summary>
	public void Step(FootstepInput input)
	{
		DidStepThisFrame = false;

		if (!input.OnFloor || input.IsSliding || input.HorizontalSpeed < Sv.FootstepMinSpeed)
		{
			_wasMoving = false;
			return;
		}

		float stride = StrideLength(input);

		if (!_wasMoving)
		{
			_wasMoving = true;
			_phase = Mathf.Floor((float)_phase) + Mathf.Clamp(Sv.FootstepInitialStepFraction, 0f, 0.99f);
		}

		int before = (int)System.Math.Floor(_phase);
		_phase += input.HorizontalSpeed * input.Dt / Mathf.Max(0.3f, stride);
		int after = (int)System.Math.Floor(_phase);
		if (after <= before) return;

		if (after - before > 1) _phase = before + 1.5;

		_leftFoot = !_leftFoot;
		StepIsLeftFoot = _leftFoot;
		StepLoudness = ComputeLoudness(input);
		DidStepThisFrame = StepLoudness > 0.001f;
	}

	/// <summary>Metres between steps. Sprint shortens it, crouch lengthens it.</summary>
	private float StrideLength(FootstepInput input)
	{
		float stride = Sv.FootstepStrideLength;
		if (input.IsSprinting) stride *= Sv.FootstepSprintStrideMul;
		else if (input.CrouchHeld) stride *= Sv.FootstepCrouchStrideMul;
		return Mathf.Max(0.3f, stride);
	}

	/// <summary>0..1 loudness. Shift is silent; otherwise speed-banded and quieter when crouched.</summary>
	private float ComputeLoudness(FootstepInput input)
	{
		if (input.ShiftHeld) return 0f;

		float speed = input.HorizontalSpeed;
		float loud;
		if (speed <= Sv.WalkSpeed)
			loud = Mathf.Lerp(Sv.FootstepMinLoudness, Sv.FootstepWalkLoudness,
				Mathf.Clamp(speed / Mathf.Max(0.01f, Sv.WalkSpeed), 0f, 1f));
		else
			loud = Mathf.Lerp(Sv.FootstepWalkLoudness, Sv.FootstepSprintLoudness,
				Mathf.Clamp((speed - Sv.WalkSpeed) / Mathf.Max(0.01f, Sv.SprintSpeed - Sv.WalkSpeed), 0f, 1f));

		if (input.CrouchHeld) loud *= Sv.FootstepCrouchLoudnessMul;
		return Mathf.Clamp(loud, 0f, 1f);
	}
}
