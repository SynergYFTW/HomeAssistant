using Figgle;
using FluentScheduler;
using Luna.Gpio;
using Luna.Gpio.Drivers;
using Luna.Logging;
using Luna.Modules;
using Luna.Modules.Interfaces;
using Luna.Server;
using Luna.Shell;
using Luna.Watchers;
using Synergy.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using static Luna.Gpio.Enums;

namespace Luna
{
    internal class Core
    {
        private readonly InternalLogger Logger;
        private readonly CancellationTokenSource KeepAliveToken = new CancellationTokenSource();
        private readonly ConfigWatcher InternalConfigWatcher;
        private readonly ModuleWatcher InternalModuleWatcher;
        private readonly GpioCore Controller;
        private readonly UpdateManager Updater;
        private readonly ModuleLoader ModuleLoader;
        private readonly RestCore RestServer;
        private readonly PinsWrapper Pins;

        internal readonly CoreConfig Config;
        internal readonly bool InitiationCompleted;
        internal readonly bool IsBaseInitiationCompleted;
        internal static bool IsNetworkAvailable => Helpers.IsNetworkAvailable();
        internal static readonly Stopwatch RuntimeSpanCounter;

        static Core()
        {
            RuntimeSpanCounter = new Stopwatch();
            JobManager.Initialize();
        }

        internal Core(string[] args)
        {
            Console.Title = "Initializing...";
            Logger = InternalLogger.GetOrCreateLogger<Core>(this, nameof(Core));
            OS.Init(true);
            RuntimeSpanCounter.Restart();
            File.WriteAllText("version.txt", Constants.Version?.ToString());

            if (File.Exists(Constants.TraceLogPath))
            {
                File.Delete(Constants.TraceLogPath);
            }

            Config = new CoreConfig(this);
            Config.LoadAsync().Wait();
            Config.LocalIP = Helpers.GetLocalIpAddress()?.ToString() ?? "-Invalid-";
            Config.PublicIP = Helpers.GetPublicIP()?.ToString() ?? "-Invalid-";

            if (!IsNetworkAvailable)
            {
                Logger.Warn("No Internet connection.");
                Logger.Info($"Starting offline mode...");
            }

            Pins = new PinsWrapper(
                    Config.GpioConfiguration.OutputModePins,
                    Config.GpioConfiguration.InputModePins,
                    Constants.BcmGpioPins,
                    Config.GpioConfiguration.RelayPins,
                    Config.GpioConfiguration.InfraredSensorPins,
                    Config.GpioConfiguration.SoundSensorPins
            );

            Controller = new GpioCore(Pins, this, Config.GpioConfiguration.GpioSafeMode);
            Updater = new UpdateManager(this);
            ModuleLoader = new ModuleLoader();
            RestServer = new RestCore(Config.RestServerPort, Config.Debug);

            JobManager.AddJob(() => SetConsoleTitle(), (s) => s.WithName("ConsoleUpdater").ToRunEvery(1).Seconds());
            Logger.CustomLog(FiggleFonts.Ogre.Render("LUNA"), ConsoleColor.Green);
            Logger.CustomLog($"---------------- Starting Luna v{Constants.Version} ----------------", ConsoleColor.Blue);
            IsBaseInitiationCompleted = true;
            PostInitiation().Wait();
            InternalConfigWatcher = new ConfigWatcher(this);
            InternalModuleWatcher = new ModuleWatcher(this);
            InitiationCompleted = true;
        }

        private async Task PostInitiation()
        {
            async void moduleLoaderAction() => await ModuleLoader.LoadAsync(Config.EnableModules).ConfigureAwait(false);

            async void checkAndUpdateAction() => await Updater.CheckAndUpdateAsync(true).ConfigureAwait(false);

            async void gpioControllerInitAction()
            {
                GpioControllerDriver? driver = default;

                switch (Config.GpioConfiguration.GpioDriverProvider)
                {
                    case GpioDriver.RaspberryIODriver:
                        driver = new RaspberryIODriver(new InternalLogger(nameof(RaspberryIODriver)), Pins, Controller.GetPinConfig(), Config.GpioConfiguration.PinNumberingScheme);
                        break;
                    case GpioDriver.SystemDevicesDriver:
                        driver = new SystemDeviceDriver(new InternalLogger(nameof(SystemDeviceDriver)), Pins, Controller.GetPinConfig(), Config.GpioConfiguration.PinNumberingScheme);
                        break;
                    case GpioDriver.WiringPiDriver:
                        driver = new WiringPiDriver(new InternalLogger(nameof(WiringPiDriver)), Pins, Controller.GetPinConfig(), Config.GpioConfiguration.PinNumberingScheme);
                        break;
                }

                await Controller.InitController(driver, Config.GpioConfiguration.PinNumberingScheme).ConfigureAwait(false);
            }

            async void restServerInitAction() => await RestServer.InitServerAsync().ConfigureAwait(false);

            static async void endStartupAction()
            {
                ModuleLoader.ExecuteActionOnType<IEvent>((e) => e.OnStarted());
            }

            Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 10 },
                moduleLoaderAction,
                checkAndUpdateAction,
                gpioControllerInitAction,
                restServerInitAction,
                endStartupAction
            );

            Interpreter.Pause();
            await Interpreter.InitInterpreterAsync().ConfigureAwait(false);
        }

        internal void OnExit()
        {
            Logger.Info("Shutting down...");

            Parallel.Invoke(
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 10
                },
                async () => await RestServer.ShutdownServer().ConfigureAwait(false),
                () => ModuleLoader.ExecuteActionOnType<IEvent>((e) => e.OnShutdownRequested()),
                () => Interpreter.ExitShell(),
                () => RestServer.Dispose(),
                () => Controller.Dispose(),
                () => JobManager.RemoveAllJobs(),
                () => JobManager.Stop(),
                () => InternalConfigWatcher.StopWatcher(),
                () => InternalModuleWatcher.StopWatcher(),
                () => ModuleLoader?.OnCoreShutdown(),
                () => Updater.Dispose(),
                async () => await Config.SaveAsync().ConfigureAwait(false)
            );

            Logger.Trace("Finished exit tasks.");
        }

        internal void ExitEnvironment(int exitCode = 0)
        {
            if (exitCode != 0)
            {
                Logger.Warn("Exiting with nonzero error code...");
            }

            if (exitCode == 0)
            {
                OnExit();
            }

            InternalLogManager.LoggerOnShutdown();
            KeepAliveToken.Cancel();
            Environment.Exit(exitCode);
        }

        internal async Task Restart(int delay = 10)
        {
            Helpers.ScheduleTask(() => "cd /home/pi/Desktop/HomeAssistant/Helpers/Restarter && dotnet RestartHelper.dll".ExecuteBash(false), TimeSpan.FromSeconds(delay));
            await Task.Delay(TimeSpan.FromSeconds(delay)).ConfigureAwait(false);
            ExitEnvironment(0);
        }

        internal async Task KeepAlive()
        {
            Logger.CustomLog($"Press {Constants.SHELL_KEY} for shell.", ConsoleColor.Green);
            while (!KeepAliveToken.Token.IsCancellationRequested)
            {
                try
                {
                    if (Interpreter.PauseShell)
                    {
                        if (!Console.KeyAvailable)
                        {
                            continue;
                        }

                        ConsoleKeyInfo pressedKey = Console.ReadKey(true);

                        switch (pressedKey.Key)
                        {
                            case Constants.SHELL_KEY:
                                Interpreter.Resume();
                                continue;

                            default:
                                continue;
                        }
                    }
                }
                finally
                {
                    await Task.Delay(1).ConfigureAwait(false);
                }
            }
        }

        private void OnCoreConfigChangeEvent(string? fileName)
        {
            if (!File.Exists(Constants.CoreConfigPath))
            {
                Logger.Log("The core config file has been deleted.", LogLevels.Warn);
                Logger.Log("Fore quitting assistant.", LogLevels.Warn);
                ExitEnvironment(0);
            }

            Logger.Log("Updating core config as the local config file as been updated...");
            Helpers.InBackgroundThread(Config.Load);
        }

        private void OnDiscordConfigChangeEvent(string? fileName)
        {
        }

        private void OnMailConfigChangeEvent(string? fileName)
        {
        }

        private void OnModuleDirectoryChangeEvent(string? absoluteFileName)
        {
            if (string.IsNullOrEmpty(absoluteFileName))
            {
                return;
            }

            string fileName = Path.GetFileName(absoluteFileName);
            string filePath = Path.GetFullPath(absoluteFileName);
            Logger.Log($"An event has been raised on module folder for file > {fileName}", LogLevels.Trace);

            if (!File.Exists(filePath))
            {
                ModuleLoader.UnloadFromPath(filePath);
                return;
            }

            Helpers.InBackground(async () => await ModuleLoader.LoadAsync(Config.EnableModules).ConfigureAwait(false));
        }

        private void SetConsoleTitle()
        {
            string text = $"Luna v{Constants.Version} | https://{Config.LocalIP}:{Config.RestServerPort}/ | {DateTime.Now.ToLongTimeString()} | ";
            text += GpioCore.IsAllowedToExecute ? $"Uptime : {Math.Round(Pi.Info.UptimeTimeSpan.TotalMinutes, 3)} minutes" : null;
            Helpers.SetConsoleTitle(text);
        }

        public GpioCore GetGpioCore() => Controller;

        public UpdateManager GetUpdater() => Updater;

        public CoreConfig GetCoreConfig() => Config;

        public ModuleLoader GetModuleInitializer() => ModuleLoader;

        internal WatcherBase GetFileWatcher() => InternalConfigWatcher;

        internal WatcherBase GetModuleWatcher() => InternalModuleWatcher;
    }
}
