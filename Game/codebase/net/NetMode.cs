namespace Vantix.Net;

/// <summary>Mode the game instance runs in — set from the command line (see NetCli.Parse).</summary>
public enum NetMode
{
	/// <summary>Server plus local client in one process — dev shortcut for editor play.</summary>
	Listen,

	/// <summary>Client only. Boots into the main menu unless AutoConnect (via <c>--connect HOST:PORT</c>)
	/// connects directly to NetCli.Host:Port.</summary>
	Client,

	/// <summary>Dedicated headless server.</summary>
	Server,
}
