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
		LastReconcileDriftM = 0f;
		ReconcilesPerSec = 0;
		LastReconcileTimeSec = 0.0;
		JitterDownMs = 0f;
		JitterUpMs = 0f;
	}
}
