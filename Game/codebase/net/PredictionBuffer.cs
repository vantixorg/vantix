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

namespace Vantix.Client;

/// <summary>Ring buffer of predicted states per tick, reconciled against server snapshots.</summary>
public class PredictionBuffer
{
	/// <summary>One prediction tick: input plus post-step state and position.</summary>
	public struct Entry
	{
		public uint Tick;
		public MovementInput Input;
		public MovementSnapshot State;
		public Vector3 PostPos;
		public Vector3 PostVel;
	}

	private const int Capacity = 512;
	private readonly Entry[] _buf = new Entry[Capacity];
	private int _head;
	private int _count;

	public int Count => _count;

	/// <summary>Maps a logical index (0 = oldest) to the array index, handling wraparound.</summary>
	private int LogicalToArray(int logical) => (_head - _count + logical + Capacity) % Capacity;

	/// <summary>Appends a tick. Discards non-monotonic ticks; overwrites the oldest once full.</summary>
	public void Push(uint tick, in MovementInput input, in MovementSnapshot state, Vector3 postPos, Vector3 postVel)
	{
		if (_count > 0)
		{
			int last = (_head - 1 + Capacity) % Capacity;
			if (tick <= _buf[last].Tick) return;
		}
		_buf[_head].Tick = tick;
		_buf[_head].Input = input;
		_buf[_head].State = state;
		_buf[_head].PostPos = postPos;
		_buf[_head].PostVel = postVel;
		_head = (_head + 1) % Capacity;
		if (_count < Capacity) _count++;
	}

	/// <summary>Binary-search by tick. On hit returns the logical index; on miss returns the
	/// lower bound (first index whose tick is &gt; <paramref name="tick"/>).</summary>
	private bool TryFindIndex(uint tick, out int logicalIndex)
	{
		int lo = 0, hi = _count - 1;
		while (lo <= hi)
		{
			int mid = (lo + hi) >> 1;
			uint midTick = _buf[LogicalToArray(mid)].Tick;
			if (midTick == tick) { logicalIndex = mid; return true; }
			if (midTick < tick) lo = mid + 1;
			else hi = mid - 1;
		}
		logicalIndex = lo;
		return false;
	}

	/// <summary>Looks up the exact tick; false if it has rolled out of the buffer.</summary>
	public bool TryGet(uint tick, out Entry entry)
	{
		if (TryFindIndex(tick, out int idx))
		{
			entry = _buf[LogicalToArray(idx)];
			return true;
		}
		entry = default;
		return false;
	}

	/// <summary>Logical index of the first entry with tick &gt; <paramref name="afterTick"/>, or Count
	/// if none. Used by reconcile replay loops.</summary>
	public int FindFirstIndexAfter(uint afterTick)
	{
		if (_count == 0) return 0;
		int lo = 0, hi = _count - 1;
		while (lo <= hi)
		{
			int mid = (lo + hi) >> 1;
			if (_buf[LogicalToArray(mid)].Tick <= afterTick) lo = mid + 1;
			else hi = mid - 1;
		}
		return lo;
	}

	/// <summary>Random access by logical index — caller checks bounds via Count.</summary>
	public Entry GetAt(int logicalIndex) => _buf[LogicalToArray(logicalIndex)];

	/// <summary>Writes back an entry's state after a replay step so later reconciliations see it.</summary>
	public void UpdateEntryState(uint tick, in MovementSnapshot newState, Vector3 newPos, Vector3 newVel)
	{
		if (!TryFindIndex(tick, out int idx)) return;
		int arr = LogicalToArray(idx);
		_buf[arr].State = newState;
		_buf[arr].PostPos = newPos;
		_buf[arr].PostVel = newVel;
	}

	/// <summary>Empties the buffer.</summary>
	public void Clear() { _count = 0; _head = 0; }
}
