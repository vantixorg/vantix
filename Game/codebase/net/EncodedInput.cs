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
