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

namespace Vantix.Net;

/// <summary>Decoded per-tick input (view, wishdir, buttons and subtick events) the simulation steps on.</summary>
public struct InputPacket
{
	public uint TickIndex;
	/// <summary>Subtick events ordered by TFraction ascending. Null/empty for tick-quantised inputs.</summary>
	public SubtickEvent[] Events;
	public ushort InitialBits;
	public float InitialViewYaw;
	public float InitialViewPitch;
	public float ViewYaw;
	public float ViewPitch;
	public float WishX;
	public float WishZ;
	public bool SprintHeld, ShiftHeld, CrouchHeld, CrouchPressed, AdsHeld, BreathHoldHeld, JumpPressed, FirePressed;
	public bool ReloadPressed, InspectPressed, SlotIsGrenade;
	/// <summary>Sub-tick offset of the fire-press edge (0..255 → 0..0.996 of a tick); only set when FirePressed.
	/// Server adds <c>FireSubTick / 256f</c> to the lag-comp rewind tick.</summary>
	public byte FireSubTick;
	public byte InterpDelayTicks;
}
