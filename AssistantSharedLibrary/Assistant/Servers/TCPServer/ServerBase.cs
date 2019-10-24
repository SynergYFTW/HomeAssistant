using AssistantSharedLibrary.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AssistantSharedLibrary.Assistant.Servers.TCPServer {
	public class ServerBase {
		private TcpListener Server { get; set; }
		private int ServerPort { get; set; }
		private bool ExitRequested { get; set; }
		private static readonly SemaphoreSlim ServerSemaphore = new SemaphoreSlim(1, 1);
		public bool IsServerListerning { get; private set; }
		internal readonly ConcurrentDictionary<string, Connection> ConnectedClients = new ConcurrentDictionary<string, Connection>();

		public delegate void OnClientConnected(object sender, OnClientConnectedEventArgs e);
		public event OnClientConnected ClientConnected;

		public delegate void OnServerStartedListerning(object sender, OnServerStartedListerningEventArgs e);
		public event OnServerStartedListerning ServerStarted;

		public delegate void OnServerShutdown(object sender, OnServerShutdownEventArgs e);
		public event OnServerShutdown ServerShutdown;

		public async Task<ServerBase> Start(int port, int backlog = 10) {
			if (port <= 0 || IsServerListerning) {
				return this;
			}

			ServerPort = port;
			try {
				await ServerSemaphore.WaitAsync().ConfigureAwait(false);
				EventLogger.LogInfo("Starting TCP Server...");
				Server = new TcpListener(new IPEndPoint(IPAddress.Any, ServerPort));
				Server.Start(backlog);

				EventLogger.LogInfo($"Server waiting for connections at port -> {ServerPort}");

				Helpers.InBackgroundThread(async () => {
					while (!ExitRequested && Server != null) {
						IsServerListerning = true;

						if (Server.Pending()) {
							TcpClient client = await Server.AcceptTcpClientAsync().ConfigureAwait(false);
							Connection clientConnection = new Connection(client, this);
							Helpers.InBackgroundThread(async () => await clientConnection.Init().ConfigureAwait(false), client.GetHashCode().ToString(), true);
						}

						await Task.Delay(1).ConfigureAwait(false);
					}

					IsServerListerning = false;
				}, this.GetHashCode().ToString(), true);

				while (!IsServerListerning) {
					await Task.Delay(1).ConfigureAwait(false);
				}

				return this;
			}
			finally {
				ServerSemaphore.Release();
			}
		}

		public async Task<bool> Shutdown() {
			ExitRequested = true;

			if (Server != null) {
				if (Server.Server.Connected) {
					Server.Stop();
				}

				while (Server.Server.Connected) {
					await Task.Delay(1).ConfigureAwait(false);
				}

				Server = null;
			}

			return true;
		}
	}
}