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
