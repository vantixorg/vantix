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

using System.Runtime.CompilerServices;
using Godot;

namespace Vantix.Utils;

/// <summary>Debug-logging gate driven by the "global/debug" project setting (read once, cached). Uses an
/// interpolated-string handler so `Dbg.Print($"hp={hp}")` allocates nothing when disabled.</summary>
public static class Dbg
{
	/// <summary>True when the project setting "global/debug" is active.</summary>
	public static bool Enabled { get; } = ProjectSettings.GetSetting("global/debug", false).AsBool();

	/// <summary>Like GD.Print but only emits when Enabled; zero-cost when disabled.</summary>
	public static void Print(ref PrintInterpolatedStringHandler handler)
	{
		if (Enabled)
			GD.Print(handler.ToStringAndClear());
	}

	/// <summary>Overload for plain string literals.</summary>
	public static void Print(string message)
	{
		if (Enabled)
			GD.Print(message);
	}
}

/// <summary>InterpolatedStringHandler for Dbg.Print. The <c>out bool shouldAppend</c> constructor
/// convention lets the compiler skip all Append calls when Dbg.Enabled is false (zero alloc).</summary>
[InterpolatedStringHandler]
public ref struct PrintInterpolatedStringHandler
{
	private DefaultInterpolatedStringHandler _inner;
	private readonly bool _enabled;

	public PrintInterpolatedStringHandler(int literalLength, int formattedCount, out bool shouldAppend)
	{
		_enabled = Dbg.Enabled;
		shouldAppend = _enabled;
		_inner = _enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
	}

	public void AppendLiteral(string s)
	{
		if (_enabled)
			_inner.AppendLiteral(s);
	}

	public void AppendFormatted<T>(T value)
	{
		if (_enabled)
			_inner.AppendFormatted(value);
	}

	public void AppendFormatted<T>(T value, string format)
	{
		if (_enabled)
			_inner.AppendFormatted(value, format);
	}

	public void AppendFormatted(string value)
	{
		if (_enabled)
			_inner.AppendFormatted(value);
	}

	public void AppendFormatted(System.ReadOnlySpan<char> value)
	{
		if (_enabled)
			_inner.AppendFormatted(value);
	}

	public string ToStringAndClear() => _enabled ? _inner.ToStringAndClear() : "";
}
