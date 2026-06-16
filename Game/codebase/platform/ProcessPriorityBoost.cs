using Godot;
using System.Diagnostics;

/// <summary>Autoload that raises process priority to High at startup. No admin on Windows; on Linux
/// maps to nice -10 (needs CAP_SYS_NICE).</summary>
public partial class ProcessPriorityBoost : Node
{
	public override void _Ready()
	{
		try
		{
			var proc = Process.GetCurrentProcess();
			ProcessPriorityClass old = proc.PriorityClass;
			proc.PriorityClass = ProcessPriorityClass.High;
			GD.Print($"[ProcessPriorityBoost] PID {proc.Id} priority: {old} → {proc.PriorityClass}");
		}
		catch (System.Exception e)
		{
			GD.PushWarning($"[ProcessPriorityBoost] Failed to set High priority: {e.Message} (on Linux needs CAP_SYS_NICE or root; on Windows should work without admin)");
		}
	}
}
