using static Assistant.Shell.InterpreterCore;

namespace Assistant.Shell.Internal {
	public readonly struct Parameter {
		public readonly string[] Parameters;
		public readonly COMMAND_CODE CommandCode;

		public Parameter(string[] parameters, COMMAND_CODE code) {
			Parameters = parameters;
			CommandCode = code;
		}
	}
}