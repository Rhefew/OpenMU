// <copyright file="ServerController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.API
{
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc;
    using MUnique.OpenMU.DataModel.Entities;
    using MUnique.OpenMU.GameLogic;
    using MUnique.OpenMU.GameServer;
    using MUnique.OpenMU.Interfaces;
    using MUnique.OpenMU.Persistence;

    /// <summary>
    /// Server API controller.
    /// </summary>
    [Route("api/")]
    public class ServerController : Controller
    {
        private IDictionary<int, IGameServer> _gameServers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerController"/> class.
        /// </summary>
        /// <param name="gameServers">The game servers.</param>
        public ServerController(IDictionary<int, IGameServer> gameServers) => this._gameServers = gameServers;

        /// <summary>
        /// Sends a global message to the specified server.
        /// </summary>
        /// <param name="id">The server id.</param>
        /// <param name="msg">The message.</param>
        [Route("send/{id=0}")]
        public async Task<IActionResult> SendGlobalMessage(int id, [FromQuery(Name = "msg")] string msg)
        {
            var server = (GameServer)this._gameServers.Values.ElementAt(id);
            if (server is not null)
            {
                await server.Context.SendGlobalNotificationAsync(msg).ConfigureAwait(false);
                return this.Ok("Done");
            }

            return this.Ok("Server not ready");
        }

        /// <summary>
        /// Gets a flag, if the specified account is currently online.
        /// </summary>
        /// <param name="accountName">Name of the account.</param>
        /// <returns>True, when online.</returns>
        [HttpGet]
        [Route("is-online/{accountName=0}")]
        public async Task<bool> GetIsOnlineAsync(string accountName)
        {
            var isOnline = false;

            foreach (var server in this._gameServers.Values.OfType<GameServer>())
            {
                var players = await server.Context.GetPlayersAsync().ConfigureAwait(false);
                if (players.Any(p => p.Account?.LoginName == accountName))
                {
                    isOnline = true;
                    break;
                }
            }

            return isOnline;
        }

        /// <summary>
        /// Kicks the specified account.
        /// </summary>
        /// <param name="accountName">Name of the account.</param>
        [HttpPost]
        [Route("accounts/kick/{accountName}")]
        public async Task<IActionResult> KickAccountAsync(string accountName)
        {
            var kicked = false;
            foreach (var server in this._gameServers.Values.OfType<GameServer>())
            {
                var players = await server.Context.GetPlayersAsync().ConfigureAwait(false);
                foreach (var p in players)
                {
                    if (p.Account?.LoginName == accountName)
                    {
                        await p.DisconnectAsync().ConfigureAwait(false);
                        kicked = true;
                        break;
                    }
                }
            }

            if (kicked)
            {
                return this.Ok("Kicked");
            }

            return this.NotFound("Account not online");
        }

        /// <summary>
        /// Kicks the specified player.
        /// </summary>
        /// <param name="playerName">Name of the player.</param>
        [HttpPost]
        [Route("players/kick/{playerName}")]
        public async Task<IActionResult> KickPlayerAsync(string playerName)
        {
            var kicked = false;
            foreach (var server in this._gameServers.Values.OfType<GameServer>())
            {
                var players = await server.Context.GetPlayersAsync().ConfigureAwait(false);
                foreach (var p in players)
                {
                    if (p.Name == playerName)
                    {
                        await p.DisconnectAsync().ConfigureAwait(false);
                        kicked = true;
                        break;
                    }
                }
            }

            if (kicked)
            {
                return this.Ok("Kicked");
            }

            return this.NotFound("Player not online");
        }

        /// <summary>
        /// Gets the server state.
        /// </summary>
        [HttpGet]
        [Route("status")]
        public IActionResult ServerState()
        {
            int sum = 0;
            var list = new List<string>();
            this._gameServers.Values.ForEach(async item =>
            {
                var server = item as GameServer;
                if (server is not null)
                {
                    await server.Context.ForEachPlayerAsync(player =>
                    {
                        list.Add(player.GetName());
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    sum = sum + server.Context.PlayerCount;
                }
            });

            var item = new
            {
                state = "Online",
                players = sum,
                playersList = list,
            };

            return this.Ok(JsonSerializer.Serialize(item));
        }
    }
}