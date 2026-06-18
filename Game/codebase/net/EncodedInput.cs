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

/// <summary>Wire-quantised form of one tick's input (packed view angles, wishdir and subtick events).</summary>
public struct EncodedInput
{
	public uint TickIndex;
	public ushort QYaw;
	public ushort QPitch;
	public short QWishX;
	public short QWishZ;
	public byte Flags1;
	public byte Flags2;
	/// <summary>Sub-tick fire-press offset (0..255 → 0..0.996 of a tick) for lag-comp rewind.
	/// Only set when Flags1 bit 7 (firePressed) is set; else 0.</summary>
	public byte FireSubTick;
	public byte InterpDelayTicks;
	/// <summary>InputBits at tick start (t=0); seeds the server's subtick replay. 0 on legacy paths.</summary>
	public ushort InitialBits;
	public ushort QInitialYaw;
	public ushort QInitialPitch;
	/// <summary>Valid entries in Events. 0 = server takes the legacy single-segment path.
	/// Capped at <see cref="Packets.MaxSubtickEventsWire"/>.</summary>
	public byte EventCount;
	/// <summary>Subtick events, length == EventCount. Null when EventCount = 0.</summary>
	public SubtickEventEncoded[] Events;
}
