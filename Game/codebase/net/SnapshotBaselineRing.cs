namespace Vantix.Net;

/// <summary>Ring of snapshot baselines for delta compression.
/// Server: one ring per peer of the last Capacity snapshots sent (post-PVS); each send deltas against the
/// entry matching PeerState.LastAckedSnapshotTick.
/// Client: ring of received + reconstructed snapshots; a packet with <c>baselineTick != 0</c> deltas onto the
/// looked-up baseline.
/// Capacity 64 (~1s @ 64Hz) tolerates ~500ms RTT; past that the baseline ages out and the next snapshot goes
/// full (self-healing).</summary>
public class SnapshotBaselineRing
{
	private const int Capacity = 64;
	private readonly Entry[] _ring = new Entry[Capacity];
	private uint _pushCount;

	/// <summary>One baseline: tick plus per-player state, used for delta decoding.</summary>
	public class Entry
	{
		public uint Tick;
		public bool Valid;
		public SnapshotPlayer[] Players = System.Array.Empty<SnapshotPlayer>();
		public int PlayerCount;
	}

	/// <summary>Stores the snapshot in the current slot. The SnapshotPlayer[] only grows when the player
	/// count rises; otherwise overwritten in place (zero-alloc steady state).</summary>
	public void Push(uint tick, System.Collections.Generic.IReadOnlyList<SnapshotPlayer> players)
	{
		int count = players.Count;
		int slot = (int)(_pushCount % Capacity);
		var e = _ring[slot] ??= new Entry();
		if (e.Players.Length < count) e.Players = new SnapshotPlayer[count];
		for (int i = 0; i < count; i++) e.Players[i] = players[i];
		e.PlayerCount = count;
		e.Tick = tick;
		e.Valid = true;
		_pushCount++;
	}

	/// <summary>Array variant (client already holds snapshots as an array).</summary>
	public void Push(uint tick, SnapshotPlayer[] players, int count)
	{
		int slot = (int)(_pushCount % Capacity);
		var e = _ring[slot] ??= new Entry();
		if (e.Players.Length < count) e.Players = new SnapshotPlayer[count];
		for (int i = 0; i < count; i++) e.Players[i] = players[i];
		e.PlayerCount = count;
		e.Tick = tick;
		e.Valid = true;
		_pushCount++;
	}

	/// <summary>Entry for the tick, or null if not in history (aged out or never sent/received).
	/// Linear scan over 64 slots.</summary>
	public Entry Find(uint tick)
	{
		for (int i = 0; i < Capacity; i++)
		{
			var e = _ring[i];
			if (e != null && e.Valid && e.Tick == tick) return e;
		}
		return null;
	}

	/// <summary>Invalidates all entries. Call on session reset (reconnect, new map).</summary>
	public void Clear()
	{
		for (int i = 0; i < Capacity; i++)
			if (_ring[i] != null) _ring[i].Valid = false;
		_pushCount = 0;
	}
}
