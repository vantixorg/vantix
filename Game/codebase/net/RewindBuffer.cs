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

namespace Vantix.Server;

/// <summary>Per-agent ring of authoritative positions per tick; Hitscan rewinds agents here for lag
/// compensation. Holds 128 ticks (1s @ 128Hz).</summary>
public class RewindBuffer
{
	/// <summary>One recorded tick/position.</summary>
	public struct Entry { public uint Tick; public Vector3 Pos; }

	private const int Capacity = 128;
	private readonly Entry[] _buf = new Entry[Capacity];
	private int _head;
	private int _count;


	private int LogicalToArray(int logical) => (_head - _count + logical + Capacity) % Capacity;

	/// <summary>Appends a tick/position; discards non-monotonic ticks, overwrites the oldest when full.</summary>
	public void Push(uint tick, Vector3 pos)
	{
		if (_count > 0)
		{
			int last = (_head - 1 + Capacity) % Capacity;
			if (tick <= _buf[last].Tick) return;
		}
		_buf[_head].Tick = tick;
		_buf[_head].Pos = pos;
		_head = (_head + 1) % Capacity;
		if (_count < Capacity) _count++;
	}

	/// <summary>Interpolated position for the tick, clamped to the nearest endpoint when out of range.
	/// Binary-searches the bracket — O(log n).</summary>
	public Vector3 Query(uint tick)
	{
		if (_count == 0) return Vector3.Zero;
		int oldestArr = LogicalToArray(0);
		int newestArr = (_head - 1 + Capacity) % Capacity;
		if (tick <= _buf[oldestArr].Tick) return _buf[oldestArr].Pos;
		if (tick >= _buf[newestArr].Tick) return _buf[newestArr].Pos;

		int lo = 0, hi = _count - 1;
		while (hi - lo > 1)
		{
			int mid = (lo + hi) >> 1;
			uint midTick = _buf[LogicalToArray(mid)].Tick;
			if (midTick == tick) return _buf[LogicalToArray(mid)].Pos;
			if (midTick < tick) lo = mid; else hi = mid;
		}
		var a = _buf[LogicalToArray(lo)];
		var b = _buf[LogicalToArray(hi)];
		uint span = b.Tick - a.Tick;
		float t = span == 0u ? 0f : (tick - a.Tick) / (float)span;
		return a.Pos.Lerp(b.Pos, t);
	}

	/// <summary>Empties the buffer.</summary>
	public void Clear() { _count = 0; _head = 0; }
}
