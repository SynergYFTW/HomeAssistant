using Luna.Watchers.Interfaces;
using Luna.Extensions;
using Luna.Logging;
using Luna.Logging.Interfaces;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using static Luna.Gpio.Enums;
using static Luna.Logging.Enums;

namespace Luna {
	[Serializable]
	public class CoreConfig {
		[JsonProperty]
		public bool AutoUpdates { get; set; } = true;

		[JsonProperty]
		public bool EnableModules { get; set; } = true;

		[JsonProperty]
		public bool GpioSafeMode { get; set; } = false;

		[JsonProperty]
		public int[] OutputModePins = new int[]
		{
			2, 3, 4, 17, 27, 22, 10, 9
		};

		[JsonProperty]
		public int[] InputModePins = new int[] {
			26,20,16
		};

		[JsonProperty]
		public int[] IRSensorPins = new int[] {
			26,20
		};

		[JsonProperty]
		public int[] SoundSensorPins = new int[] {
			16
		};

		[JsonProperty]
		public int[] RelayPins = new int[] {
			2, 3, 4, 17, 27, 22, 10, 9
		};

		[JsonProperty]
		public bool Debug { get; set; } = false;

		[JsonProperty]
		public int RestServerPort { get; set; } = 7577;

		[JsonProperty]
		public string? StatisticsServerIP { get; set; }

		[JsonProperty]
		public string? OpenWeatherApiKey { get; set; }

		[JsonProperty]
		public string? PushBulletApiKey { get; set; }

		[JsonProperty]
		public string AssistantDisplayName { get; set; } = "Home Assistant";

		[JsonProperty]
		public GpioDriver GpioDriverProvider { get; set; } = GpioDriver.RaspberryIODriver;

		[JsonProperty]
		public NumberingScheme PinNumberingScheme { get; set; } = NumberingScheme.Logical;

		[JsonProperty]
		public DateTime ProgramLastStartup { get; set; }

		[JsonProperty]
		public DateTime ProgramLastShutdown { get; set; }

		private readonly ILogger Logger = new Logger(typeof(CoreConfig).Name);
		private readonly SemaphoreSlim ConfigSemaphore = new SemaphoreSlim(1, 1);
		private readonly Core Core;

		[JsonConstructor]
		internal CoreConfig() { }

		internal CoreConfig(Core _core) => Core = _core ?? throw new ArgumentNullException(nameof(_core));

		internal void Save() {
			if (!Directory.Exists(Constants.ConfigDirectory)) {
				Logger.Log("Config folder doesn't exist, creating one...");
				Directory.CreateDirectory(Constants.ConfigDirectory);
			}

			ConfigSemaphore.Wait();
			Core.GetFileWatcher().Pause();

			Logger.Log("Saving core config...", LogLevels.Trace);

			try {
				string filePath = Constants.CoreConfigPath;
				string json = JsonConvert.SerializeObject(this, Formatting.Indented);
				string newFilePath = filePath + ".new";

				using (StreamWriter writer = new StreamWriter(newFilePath)) {
					writer.Write(json);
					writer.Flush();
				}

				if (File.Exists(filePath)) {
					File.Replace(newFilePath, filePath, null);
				}
				else {
					File.Move(newFilePath, filePath);
				}
			}
			catch (Exception e) {
				Logger.Log(e);
				return;
			}
			finally {
				ConfigSemaphore.Release();
				Core.GetFileWatcher().Resume();
			}

			Logger.Log("Saved core config!", LogLevels.Trace);
		}

		internal void Load() {
			if (!Directory.Exists(Constants.ConfigDirectory)) {
				Logger.Log("Config folder doesn't exist, creating one...");
				Directory.CreateDirectory(Constants.ConfigDirectory);
			}

			if (!File.Exists(Constants.CoreConfigPath) && !GenerateDefaultConfig()) {
				return;
			}

			ConfigSemaphore.Wait();

			try {
				Logger.Log("Loading core config...", LogLevels.Trace);
				using (StreamReader reader = new StreamReader(new FileStream(Constants.CoreConfigPath, FileMode.Open, FileAccess.Read))) {
					string jsonContent = reader.ReadToEnd();

					if (string.IsNullOrEmpty(jsonContent)) {
						return;
					}

					CoreConfig config = JsonConvert.DeserializeObject<CoreConfig>(jsonContent);

					this.AssistantDisplayName = config.AssistantDisplayName;
					this.AutoUpdates = config.AutoUpdates;
					this.Debug = config.Debug;
					this.EnableModules = config.EnableModules;
					this.GpioSafeMode = config.GpioSafeMode;
					this.InputModePins = config.InputModePins;
					this.IRSensorPins = config.IRSensorPins;
					this.OpenWeatherApiKey = config.OpenWeatherApiKey;
					this.OutputModePins = config.OutputModePins;
					this.ProgramLastShutdown = config.ProgramLastShutdown;
					this.ProgramLastStartup = config.ProgramLastStartup;
					this.PushBulletApiKey = config.PushBulletApiKey;
					this.RelayPins = config.RelayPins;
					this.SoundSensorPins = config.SoundSensorPins;
					this.StatisticsServerIP = config.StatisticsServerIP;
				}

				Logger.Log("Core configuration loaded successfully!", LogLevels.Trace);
			}
			catch (Exception e) {
				Logger.Log(e);
				return;
			}
			finally {
				ConfigSemaphore.Release();
			}
		}

		internal bool GenerateDefaultConfig() {
			Logger.Log("Generating default Config...");
			if (!Directory.Exists(Constants.ConfigDirectory)) {
				Logger.Log("Config directory doesn't exist, creating one...");
				Directory.CreateDirectory(Constants.ConfigDirectory);
			}

			if (File.Exists(Constants.CoreConfigPath)) {
				return true;
			}

			Save();
			return true;
		}		

		public override string? ToString() => base.ToString();

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public static bool operator ==(CoreConfig left, CoreConfig right) => Equals(left, right);

		public static bool operator !=(CoreConfig left, CoreConfig right) => !Equals(left, right);
	}
}