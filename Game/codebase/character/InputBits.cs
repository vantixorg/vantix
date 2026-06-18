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

/// <summary>
/// Held-input bitfield for subtick movement; a bit is set while its key is down. No "pressed" bit:
/// the driver detects press-edges from the 0→1 transition between consecutive SubtickEvent.StateAfter masks.
/// </summary>
[System.Flags]
public enum InputBits : ushort
{
	None = 0,
	Forward = 1 << 0,
	Back = 1 << 1,
	Left = 1 << 2,
	Right = 1 << 3,
	Jump = 1 << 4,
	Crouch = 1 << 5,
	Sprint = 1 << 6,
	ShiftWalk = 1 << 7,
	Fire = 1 << 8,
	Ads = 1 << 9,
	Reload = 1 << 10,
	Inspect = 1 << 11,
	BreathHold = 1 << 12,
}
