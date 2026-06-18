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

namespace Vantix.Net;

/// <summary>Global netcode stats. Written by NetClient/NetServer, read by DebugOverlay; static to avoid
/// per-node wiring. Server mode fills only server fields, Client only client fields, Listen both.</summary>
public static class NetStats
{
	public static NetMode Mode = NetMode.Listen;
	public static bool ServerRunning;
	public static bool ClientConnected;

	public static int PingMs;
	public static float PacketLossUpPct;
	public static float PacketLossDownPct;
	public static int BytesPerSecUp;
	public static int BytesPerSecDown;
	public static uint ClientTick;
	public static uint ServerTickEstimate;
	public static int InterpDelayMs;
	/// <summary>Last lag-comp rewind distance the server applied for this client's shot (ticks = RTT/2 + reported
	/// interp, capped at sv_max_unlag_ticks). Listen/debug only; mirrors how far hitscan rewound opponents.</summary>
	public static int LagCompRewindTicks;

	public static int PeerCount;
	public static int MaxPlayers;
	public static uint ServerTick;

	/// <summary>Last reconcile drift in metres (server pos vs. client prediction at the ack'd tick). 0 = no
	/// correction since spawn. Drives severity colour coding in the debug overlays.</summary>
	public static float LastReconcileDriftM;
	/// <summary>Horizontal (XZ) reconcile drift, metres. Aim-relevant — should stay tight.</summary>
	public static float LastReconcileDriftHorizM;
	/// <summary>Vertical (Y) reconcile drift, metres. Mostly cosmetic stair-step mismatch; ~20cm tolerable.</summary>
	public static float LastReconcileDriftVertM;
	/// <summary>Rolling reconcile count over the last ~1 s. 0 = stable.</summary>
	public static int ReconcilesPerSec;
	/// <summary>Engine time (sec) of the last reconcile — for the "recent" highlight.</summary>
	public static double LastReconcileTimeSec;

	/// <summary>Snapshot inter-arrival variance, ms.</summary>
	public static float JitterDownMs;
	/// <summary>Input send-interval variance, ms (client → server).</summary>
	public static float JitterUpMs;

	/// <summary>Called once on mode change — clears stale values.</summary>
	public static void Reset(NetMode mode)
	{
		Mode = mode;
		ServerRunning = false;
		ClientConnected = false;
		PingMs = 0;
		PacketLossUpPct = 0f;
		PacketLossDownPct = 0f;
		BytesPerSecUp = 0;
		BytesPerSecDown = 0;
		ClientTick = 0u;
		ServerTickEstimate = 0u;
		ServerTick = 0u;
		PeerCount = 0;
		InterpDelayMs = 100;
		LagCompRewindTicks = 0;
		LastReconcileDriftM = 0f;
		ReconcilesPerSec = 0;
		LastReconcileTimeSec = 0.0;
		JitterDownMs = 0f;
		JitterUpMs = 0f;
	}
}
