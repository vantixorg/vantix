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

namespace Vantix.Character;

/// <summary>
/// Per-tick bone pose history per player (hitbox GlobalTransforms). Lets lag-comp rewind animated
/// bone positions, so headshots land when server/client animation differ by a frame or two.
/// Buffers 128 ticks (~1s at 128Hz).
/// </summary>
public class BonePoseRewindBuffer
{
	private struct Entry { public uint Tick; public Transform3D[] Transforms; }
	public const int BufferSize = 128;
	private readonly Entry[] _ring = new Entry[BufferSize];
	private int _writeIdx;
	private int _count;
	private int _hitboxCount;
	/// <summary>Reusable result buffer for QueryFractional (no per-shot alloc). Consume synchronously;
	/// don't cache the reference or pass it to async code.</summary>
	private Transform3D[] _fractionalResult;

	/// <summary>Allocates the per-slot Transform3D arrays. Call once after HitboxRig.Build().</summary>
	public void Init(int hitboxCount)
	{
		_hitboxCount = hitboxCount;
		for (int i = 0; i < BufferSize; i++)
			_ring[i].Transforms = new Transform3D[hitboxCount];
		_fractionalResult = new Transform3D[hitboxCount];
	}

	/// <summary>Snapshots all CollisionShape3D GlobalTransforms into the ring at this tick. Uses the shape
	/// transform, not the hitbox, since auto-orient offsets the shape from the hitbox origin.</summary>
	public void Push(uint tick, System.Collections.Generic.IReadOnlyList<CollisionShape3D> shapes)
	{
		if (_hitboxCount == 0 || shapes.Count != _hitboxCount) return;
		var entry = _ring[_writeIdx];
		entry.Tick = tick;
		for (int i = 0; i < _hitboxCount; i++)
			entry.Transforms[i] = shapes[i] != null ? shapes[i].GlobalTransform : Transform3D.Identity;
		_ring[_writeIdx] = entry;
		_writeIdx = (_writeIdx + 1) % BufferSize;
		if (_count < BufferSize) _count++;
	}

	/// <summary>Snapshot from the newest tick that isn't newer than <paramref name="tick"/>. Null when
	/// no history yet (freshly spawned).</summary>
	public Transform3D[] Query(uint tick)
	{
		if (_count == 0) return null;
		for (int i = 0; i < _count; i++)
		{
			int idx = (_writeIdx - 1 - i + BufferSize) % BufferSize;
			ref var e = ref _ring[idx];
			if (e.Tick <= tick) return e.Transforms;
		}
		int oldestIdx = _count < BufferSize ? 0 : _writeIdx;
		return _ring[oldestIdx].Transforms;
	}

	/// <summary>Interpolates each bone transform between the two ticks bracketing
	/// <paramref name="fractionalTick"/>, clamping to the nearest endpoint when out of range. Null only
	/// when the buffer is empty. Returns the shared _fractionalResult buffer — consume synchronously,
	/// never cache or pass to async code.</summary>
	public Transform3D[] QueryFractional(float fractionalTick)
	{
		if (_count == 0) return null;
		if (fractionalTick <= 0f) return Query(0u);

		int newestIdx = (_writeIdx - 1 + BufferSize) % BufferSize;
		int oldestIdx = _count < BufferSize ? 0 : _writeIdx;

		if (fractionalTick >= (float)_ring[newestIdx].Tick) return _ring[newestIdx].Transforms;
		if (fractionalTick <= (float)_ring[oldestIdx].Tick) return _ring[oldestIdx].Transforms;

		int hiIdx = newestIdx;
		int loIdx = newestIdx;
		for (int i = 1; i < _count; i++)
		{
			hiIdx = loIdx;
			loIdx = (_writeIdx - 1 - i + BufferSize) % BufferSize;
			if ((float)_ring[loIdx].Tick <= fractionalTick && fractionalTick <= (float)_ring[hiIdx].Tick)
				break;
		}

		ref var a = ref _ring[loIdx];
		ref var b = ref _ring[hiIdx];
		float span = b.Tick - a.Tick;
		float f = span < 1e-5f ? 0f : Mathf.Clamp((fractionalTick - a.Tick) / span, 0f, 1f);
		for (int i = 0; i < _hitboxCount; i++)
			_fractionalResult[i] = a.Transforms[i].InterpolateWith(b.Transforms[i], f);
		return _fractionalResult;
	}
}
