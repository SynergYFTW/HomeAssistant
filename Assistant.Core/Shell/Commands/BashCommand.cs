using Assistant.Extensions.Shared.Shell;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assistant.Core.Shell.Commands {
	public class BashCommand : IShellCommand, IDisposable {
		public bool HasParameters => true;

		public string CommandName => "Bash Command";

		public string CommandKey => "bash";

		public string CommandDescription => "Execute Bash scripts files via Assistant Shell.";

		public Func<Parameter, bool> OnExecuteFunc { get; set; }

		public int MaxParameterCount => 1;

		public ShellEnum.COMMAND_CODE CommandCode => ShellEnum.COMMAND_CODE.BASH_SCRIPT_PATH;

		private readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);

		public void Dispose() {
			Sync.Dispose();
		}

		public async Task ExecuteAsync(Parameter parameter) {
			if (parameter.Parameters == null || parameter.Parameters.Length <= 0) {
				return;
			}

			if (OnExecuteFunc != null) {
				if (OnExecuteFunc.Invoke(parameter)) {
					return;
				}
			}

			//TODO: bash command
		}

		public async Task InitAsync() {
			
		}

		public bool Parse(Parameter parameter) {
			return false;
		}
	}
}