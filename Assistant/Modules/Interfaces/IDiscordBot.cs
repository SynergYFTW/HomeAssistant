
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

using Discord.WebSocket;
using System.Threading.Tasks;

namespace Assistant.Modules.Interfaces {

	public interface IDiscordBot : IModuleBase, IDiscordLogger {

		/// <summary>
		/// The discord bot client
		/// </summary>
		/// <value></value>
		DiscordSocketClient Client { get; set; }

		/// <summary>
		/// The discord bot config
		/// </summary>
		/// <value></value>
		IDiscordBotConfig BotConfig { get; set; }

		/// <summary>
		/// Status if the bot is online or offline
		/// </summary>
		/// <value></value>
		bool IsServerOnline { get; set; }

		/// <summary>
		/// Stop discord client
		/// </summary>
		/// <returns></returns>
		Task<bool> StopServer();

		/// <summary>
		/// Start discord bot
		/// </summary>
		/// <returns></returns>
		Task<(bool, IDiscordBot)> RegisterDiscordClient();

		/// <summary>
		/// Restart discord bot service
		/// </summary>
		/// <returns></returns>
		Task RestartDiscordServer();
	}
}
