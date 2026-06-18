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
