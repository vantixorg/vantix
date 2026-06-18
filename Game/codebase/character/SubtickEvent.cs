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

/// <summary>An input change at a fractional position within a tick, for subtick movement replay.</summary>
public struct SubtickEvent
{
	/// <summary>Position inside the tick, 0..1 = tick-start..tick-end. Events must be sorted ascending.</summary>
	public float TFraction;

	/// <summary>Held-state bitmask after this event applies, until the next event.</summary>
	public InputBits StateAfter;

	/// <summary>View yaw for the substep starting here.</summary>
	public float ViewYaw;

	/// <summary>View pitch for the substep starting here.</summary>
	public float ViewPitch;
}
