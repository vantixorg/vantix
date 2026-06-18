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

namespace Vantix.Character;

/// <summary>Per-tick locomotion state the footstep system reads to time and pick footstep sounds.</summary>
public struct FootstepInput
{
	public float Dt;
	public float HorizontalSpeed;
	public bool OnFloor;
	public bool ShiftHeld;
	public bool CrouchHeld;
	public bool IsSprinting;
	public bool IsSliding;
}
