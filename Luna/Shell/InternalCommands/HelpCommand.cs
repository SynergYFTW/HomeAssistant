using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Luna.Shell.InternalCommands {
	public class HelpCommand : IShellCommand, IDisposable {
		public bool HasParameters => true;

		public string CommandName => "Help Command";

		public string CommandKey => "help";

		public string CommandDescription => "Displays information of the specified command.";

		public Func<Parameter, bool> OnExecuteFunc { get; set; }

		public int MaxParameterCount => 1;
		public ShellEnum.COMMAND_CODE CommandCode => ShellEnum.COMMAND_CODE.HELP_ADVANCED;

		public bool IsInitSuccess { get; set; }

		public SemaphoreSlim Sync { get; set; }

		public void Dispose() {
			IsInitSuccess = false;
			Sync.Dispose();
		}

		public async Task ExecuteAsync(Parameter parameter) {
			if (!IsInitSuccess) {
				return;
			}

			if (parameter.Parameters.Length > MaxParameterCount) {
				ShellIO.Error("Too many arguments.");
				return;
			}

			await Sync.WaitAsync().ConfigureAwait(false);

			try {
				if (OnExecuteFunc != null) {
					if (OnExecuteFunc.Invoke(parameter)) {
						return;
					}
				}

				switch (parameter.ParameterCount) {
					case 0:
						foreach (KeyValuePair<string, IShellCommand> cmd in Interpreter.Commands) {
							if (string.IsNullOrEmpty(cmd.Value.CommandKey) || string.IsNullOrEmpty(cmd.Value.CommandName)) {
								continue;
							}

							cmd.Value.OnHelpExec(true);
						}
						return;
					case 1 when !string.IsNullOrEmpty(parameter.Parameters[0]) && parameter.Parameters[0].Equals("all", StringComparison.OrdinalIgnoreCase):
						PrintAll();
						return;
					case 1 when !string.IsNullOrEmpty(parameter.Parameters[0]):
						IShellCommand shellCmd = await Interpreter.Init.GetCommandWithKeyAsync<IShellCommand>(parameter.Parameters[0]).ConfigureAwait(false);
						if (shellCmd == null) {
							ShellIO.Error("Command doesn't exist. use ' help -all ' to check all available commands!");
							return;
						}

						shellCmd.OnHelpExec(false);
						return;
					default:
						ShellIO.Error("Command seems to be in incorrect syntax.");
						return;
				}
			}
			catch (Exception e) {
				ShellIO.Exception(e);
				return;
			}
			finally {
				Sync.Release();
			}
		}

		private void PrintAll() {
			if (Interpreter.CommandsCount <= 0) {
				ShellIO.Error("No commands exist.");
				return;
			}

			ShellIO.Info("--------------------------------------- Shell Commands ---------------------------------------");
			foreach (KeyValuePair<string, IShellCommand> cmd in Interpreter.Commands) {
				if (string.IsNullOrEmpty(cmd.Key) || cmd.Value == null) {
					continue;
				}

				cmd.Value.OnHelpExec(false);
			}
			ShellIO.Info("----------------------------------------------------------------------------------------------");
		}

		public async Task InitAsync() {
			Sync = new SemaphoreSlim(1, 1);
			IsInitSuccess = true;
		}

		public bool Parse(Parameter parameter) {
			if (!IsInitSuccess) {
				return false;
			}

			return false;
		}

		public void OnHelpExec(bool quickHelp) {
			if (quickHelp) {
				ShellIO.Info($"{CommandName} - {CommandKey} | {CommandDescription} | {CommandKey};");
				return;
			}

			ShellIO.Info($"----------------- { CommandName} | {CommandKey} -----------------");
			ShellIO.Info($"|> {CommandDescription}");
			ShellIO.Info($"Basic Syntax -> ' {CommandKey} '");
			ShellIO.Info($"All Commands -> ' {CommandKey} -all '");
			ShellIO.Info($"Advanced -> ' {CommandKey} -[command_key] '");
			ShellIO.Info($"----------------- ----------------------------- -----------------");
		}
	}
}
