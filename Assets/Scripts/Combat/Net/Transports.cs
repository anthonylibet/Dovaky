using System;
using System.Collections.Generic;

namespace Dovaky.Combat.Net
{
    /// <summary>
    /// Vue du réseau côté serveur. Volontairement réduite à l'essentiel :
    /// brancher Mirror, Netcode for GameObjects ou des sockets bruts ne
    /// demande qu'une implémentation de cette interface, sans toucher au
    /// serveur de combat ni au cœur de règles.
    /// </summary>
    public interface IServerTransport
    {
        /// <summary>Message reçu d'un joueur : (identifiant du joueur, charge utile).</summary>
        event Action<int, byte[]> MessageReceived;

        IReadOnlyList<int> ConnectedPlayers { get; }

        void Send(int playerId, byte[] payload);

        void Broadcast(byte[] payload);
    }

    /// <summary>Vue du réseau côté client.</summary>
    public interface IClientTransport
    {
        event Action<byte[]> MessageReceived;

        void Send(byte[] payload);
    }

    /// <summary>
    /// Réseau en mémoire : la livraison est immédiate et synchrone. Sert aux
    /// tests et au mode « host » (un joueur héberge la partie et y joue), où
    /// le serveur et un client vivent dans le même processus.
    ///
    /// Un vrai transport livrera les messages de façon asynchrone ; le serveur
    /// et le client n'en font pas l'hypothèse — ils n'exigent que l'ordre des
    /// messages d'un même pair.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private readonly Dictionary<int, LoopbackClientTransport> _clients = new Dictionary<int, LoopbackClientTransport>();
        private readonly LoopbackServerTransport _server;

        public LoopbackNetwork()
        {
            _server = new LoopbackServerTransport(this);
        }

        public IServerTransport Server => _server;

        public IClientTransport ConnectClient(int playerId)
        {
            if (_clients.ContainsKey(playerId))
                throw new InvalidOperationException("Joueur déjà connecté : " + playerId);

            var client = new LoopbackClientTransport(this, playerId);
            _clients.Add(playerId, client);
            return client;
        }

        public void Disconnect(int playerId) => _clients.Remove(playerId);

        internal IReadOnlyList<int> PlayerIds
        {
            get
            {
                var ids = new List<int>(_clients.Keys);
                ids.Sort();
                return ids;
            }
        }

        internal void DeliverToClient(int playerId, byte[] payload)
        {
            if (_clients.TryGetValue(playerId, out LoopbackClientTransport client)) client.Deliver(payload);
        }

        internal void DeliverToServer(int playerId, byte[] payload) => _server.Deliver(playerId, payload);

        private sealed class LoopbackServerTransport : IServerTransport
        {
            private readonly LoopbackNetwork _network;

            public LoopbackServerTransport(LoopbackNetwork network)
            {
                _network = network;
            }

            public event Action<int, byte[]> MessageReceived;

            public IReadOnlyList<int> ConnectedPlayers => _network.PlayerIds;

            public void Send(int playerId, byte[] payload) => _network.DeliverToClient(playerId, payload);

            public void Broadcast(byte[] payload)
            {
                var players = _network.PlayerIds;
                for (int i = 0; i < players.Count; i++) _network.DeliverToClient(players[i], payload);
            }

            internal void Deliver(int playerId, byte[] payload) => MessageReceived?.Invoke(playerId, payload);
        }

        private sealed class LoopbackClientTransport : IClientTransport
        {
            private readonly LoopbackNetwork _network;
            private readonly int _playerId;

            public LoopbackClientTransport(LoopbackNetwork network, int playerId)
            {
                _network = network;
                _playerId = playerId;
            }

            public event Action<byte[]> MessageReceived;

            public void Send(byte[] payload) => _network.DeliverToServer(_playerId, payload);

            internal void Deliver(byte[] payload) => MessageReceived?.Invoke(payload);
        }
    }
}
