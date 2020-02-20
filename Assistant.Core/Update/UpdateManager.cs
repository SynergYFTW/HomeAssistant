using Assistant.Extensions;
using Assistant.Logging;
using Assistant.Logging.Interfaces;
using FluentScheduler;
using RestSharp;
using RestSharp.Extensions;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static Assistant.Logging.Enums;

namespace Assistant.Core.Update {
	public class UpdateManager {
		private const string JOB_NAME = "GITHUB_UPDATER";
		private readonly ILogger Logger = new Logger(typeof(UpdateManager).Name);
		private readonly GitHub Github = new GitHub();
		public bool UpdateAvailable { get; private set; } = false;
		public bool IsOnPrerelease { get; private set; } = false;
		public DateTime NextUpdateCheck => JobManager.GetSchedule(JOB_NAME).NextRun;
		private static readonly SemaphoreSlim UpdateSemaphore = new SemaphoreSlim(1, 1);

		public async Task<Version?> CheckAndUpdateAsync(bool withTimer) {
			if (!Core.IsNetworkAvailable) {
				return null;
			}

			if (!Core.Config.AutoUpdates) {
				Logger.Log("Updates are disabled.", LogLevels.Trace);
				return null;
			}

			await UpdateSemaphore.WaitAsync().ConfigureAwait(false);
			Logger.Log("Checking for any new version...", LogLevels.Trace);

			try {
				await Github.Request().ConfigureAwait(false);
				string? gitVersion = Github.ReleaseTagName;

				if (string.IsNullOrEmpty(gitVersion)) {
					Logger.Warning("Failed to request version information.");
					return null;
				}

				if (!Version.TryParse(gitVersion, out Version? LatestVersion)) {
					Logger.Log("Could not parse the version. Make sure the version is correct at Github project repo.", LogLevels.Warn);
					return null;
				}

				UpdateAvailable = LatestVersion > Constants.Version;
				IsOnPrerelease = LatestVersion < Constants.Version;

				if (!UpdateAvailable) {
					Logger.Log($"You are up to date! ({LatestVersion}/{Constants.Version})");

					if (withTimer) {
						if(JobManager.GetSchedule(JOB_NAME) == null) {
							JobManager.AddJob(async () => await CheckAndUpdateAsync(withTimer).ConfigureAwait(false), (s) => s.WithName(JOB_NAME).ToRunEvery(1).Days().At(00, 00));
						}
					}

					return LatestVersion;
				}

				if (IsOnPrerelease) {
					Logger.Log("Seems like you are on a pre-release channel. please report any bugs you encounter!", LogLevels.Warn);
					return LatestVersion;
				}

				Logger.Log($"New version available!", LogLevels.Green);
				Logger.Log($"Latest Version: {LatestVersion} / Local Version: {Constants.Version}");
				Logger.Log("Automatically updating in 10 seconds...", LogLevels.Warn);
				await Core.ModuleLoader.ExecuteAsyncEvent(Modules.ModuleInitializer.MODULE_EXECUTION_CONTEXT.UpdateAvailable).ConfigureAwait(false);
				Helpers.ScheduleTask(async () => await InitUpdate().ConfigureAwait(false), TimeSpan.FromSeconds(10));
				return LatestVersion;
			}
			catch (Exception e) {
				Logger.Exception(e);
				return null;
			}
			finally {
				UpdateSemaphore.Release();
			}
		}

		public async Task<bool> InitUpdate() {
			if (Github == null || Github.Assets == null || Github.Assets.Length <= 0 || Github.Assets[0] == null) {
				return false;
			}

			await UpdateSemaphore.WaitAsync().ConfigureAwait(false);
			int releaseID = Github.Assets[0].AssetId;
			Logger.Log($"Release name: {Github.ReleaseFileName}");
			Logger.Log($"URL: {Github.ReleaseUrl}", LogLevels.Trace);
			Logger.Log($"Version: {Github.ReleaseTagName}", LogLevels.Trace);
			Logger.Log($"Publish time: {Github.PublishedAt.ToLongTimeString()}");
			Logger.Log($"ZIP URL: {Github.Assets[0].AssetDownloadUrl}", LogLevels.Trace);
			Logger.Log($"Downloading {Github.ReleaseFileName}.zip...");

			if (File.Exists(Constants.UpdateZipFileName)) {
				File.Delete(Constants.UpdateZipFileName);
			}

			RestClient client = new RestClient($"{Constants.GitHubAssetDownloadURL}/{releaseID}");
			RestRequest request = new RestRequest(Method.GET);
			client.UserAgent = Constants.GitHubProjectName;
			request.AddHeader("cache-control", "no-cache");
			request.AddHeader("Accept", "application/octet-stream");
			IRestResponse response = client.Execute(request);

			if (response.StatusCode != HttpStatusCode.OK) {
				Logger.Log("Failed to download. Status Code: " + response.StatusCode + "/" + response.ResponseStatus.ToString());
				UpdateSemaphore.Release();
				return false;
			}

			response.RawBytes.SaveAs(Constants.UpdateZipFileName);

			Logger.Log("Successfully Downloaded, Starting update process...");
			await Task.Delay(2000).ConfigureAwait(false);

			if (!File.Exists(Constants.UpdateZipFileName)) {
				Logger.Log("Something unknown and fatal has occurred during update process. unable to proceed.", LogLevels.Error);
				UpdateSemaphore.Release();
				return false;
			}

			if (Directory.Exists(Constants.BackupDirectoryPath)) {
				Directory.Delete(Constants.BackupDirectoryPath, true);
				Logger.Log("Deleted old backup folder and its contents.");
			}

			if (OS.IsUnix) {
				if (string.IsNullOrEmpty(Constants.HomeDirectory)) {
					return false;
				}

				string executable = Path.Combine(Constants.HomeDirectory, Constants.GitHubProjectName);

				if (File.Exists(executable)) {
					OS.UnixSetFileAccessExecutable(executable);
					Logger.Log("File Permission set successfully!");
				}
			}

			UpdateSemaphore.Release();
			await Task.Delay(1000).ConfigureAwait(false);
			await Core.ModuleLoader.ExecuteAsyncEvent(Modules.ModuleInitializer.MODULE_EXECUTION_CONTEXT.UpdateStarted).ConfigureAwait(false);
			"cd /home/pi/Desktop/HomeAssistant/Helpers/Updater && dotnet Assistant.Updater.dll".ExecuteBash(true);
			await Core.Restart(5).ConfigureAwait(false);
			return true;
		}
	}
}