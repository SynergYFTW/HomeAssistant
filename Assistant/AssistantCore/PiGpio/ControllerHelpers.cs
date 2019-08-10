
//    _  _  ___  __  __ ___     _   ___ ___ ___ ___ _____ _   _  _ _____
//   | || |/ _ \|  \/  | __|   /_\ / __/ __|_ _/ __|_   _/_\ | \| |_   _|
//   | __ | (_) | |\/| | _|   / _ \\__ \__ \| |\__ \ | |/ _ \| .` | | |
//   |_||_|\___/|_|  |_|___| /_/ \_\___/___/___|___/ |_/_/ \_\_|\_| |_|
//

//MIT License

//Copyright(c) 2019 Arun Prakash
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;
using System.Text;
using Assistant.Log;
using Unosquare.RaspberryIO;

namespace Assistant.AssistantCore.PiGpio {
	public static class ControllerHelpers {

		private static readonly Logger Logger = new Logger("PI-HELPERS");

		public static void DisplayPiInfo() {
			Logger.Log($"OS: {Pi.Info.OperatingSystem.SysName}", Enums.LogLevels.Trace);
			Logger.Log($"Processor count: {Pi.Info.ProcessorCount}", Enums.LogLevels.Trace);
			Logger.Log($"Model name: {Pi.Info.ModelName}", Enums.LogLevels.Trace);
			Logger.Log($"Release name: {Pi.Info.OperatingSystem.Release}", Enums.LogLevels.Trace);
			Logger.Log($"Board revision: {Pi.Info.BoardRevision}", Enums.LogLevels.Trace);
			Logger.Log($"Pi Version: {Pi.Info.RaspberryPiVersion.ToString()}", Enums.LogLevels.Trace);
			Logger.Log($"Memory size: {Pi.Info.MemorySize.ToString()}", Enums.LogLevels.Trace);
			Logger.Log($"Serial: {Pi.Info.Serial}", Enums.LogLevels.Trace);
			Logger.Log($"Pi Uptime: {Math.Round(Pi.Info.UptimeTimeSpan.TotalMinutes, 4)} minutes", Enums.LogLevels.Trace);
		}
	}
}
