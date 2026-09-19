using System;
using System.Collections.Generic;

namespace Dovaky.Combat.Net
{
    /// <summary>
    /// Autorité de la partie. Reçoit les commandes des joueurs, vérifie
    /// qu'elles sont légitimes, les applique au combat et diffuse les
    /// événements produits.
    ///
    /// Deux contrôles sont faits ici, et nulle part ailleurs :
    /// le joueur pilote-t-il bien ce combattant, et le message est-il
    /// exploitable. Le reste des règles appartient à <see cref="Battle"/>.
    /// Un client compromis ne peut donc rien obtenir de plus qu'un client
    /// honnête.
    /// </summary>
    public sealed class BattleServer
    {
        private readonly IServerTransport _transport;
        private readonly Dictionary<int, HashSet<int>> _fightersByPlayer = new Dictionary<int, HashSet<int>>();

        public Battle Battle { get; }

        /// <summary>Commande refusée : (joueur, raison). Utile pour journaliser les abus.</summary>
        public event Action<int, CommandError> CommandRejected;

        /// <summary>Message inexploitable reçu d'un joueur : candidat à la déconnexion.</summary>
        public event Action<int, ProtocolException> ProtocolViolation;

        public BattleServer(
            Battle battle,
            IServerTransport transport,
            IReadOnlyDictionary<int, IReadOnlyList<int>> fightersByPlayer)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (fightersByPlayer == null) throw new ArgumentNullException(nameof(fightersByPlayer));

            foreach (KeyValuePair<int, IReadOnlyList<int>> entry in fightersByPlayer)
            {
                var owned = new HashSet<int>();
                foreach (int fighterId in entry.Value) owned.Add(fighterId);
                _fightersByPlayer[entry.Key] = owned;
            }

            _transport.MessageReceived += OnMessageReceived;
        }

        /// <summary>Démarre le combat et met tous les clients à niveau.</summary>
        public void Start()
        {
            IReadOnlyList<BattleEvent> events = Battle.Start();

            var players = _transport.ConnectedPlayers;
            for (int i = 0; i < players.Count; i++) SendSnapshotTo(players[i]);

            _transport.Broadcast(BattleCodec.EncodeEvents(events));
        }

        /// <summary>Renvoie l'état complet à un joueur : arrivée ou reconnexion.</summary>
        public void SendSnapshotTo(int playerId)
        {
            _transport.Send(playerId, BattleCodec.EncodeSnapshot(BattleSnapshot.Capture(Battle)));
        }

        public bool Owns(int playerId, int fighterId)
        {
            return _fightersByPlayer.TryGetValue(playerId, out HashSet<int> owned) && owned.Contains(fighterId);
        }

        private void OnMessageReceived(int playerId, byte[] payload)
        {
            BattleCommand command;
            try
            {
                if (BattleCodec.PeekType(payload) != MessageType.Command)
                    throw new ProtocolException("Un client ne peut envoyer que des commandes.");

                command = BattleCodec.DecodeCommand(payload);
            }
            catch (ProtocolException exception)
            {
                ProtocolViolation?.Invoke(playerId, exception);
                return;
            }

            // Contrôle de propriété : c'est ce qui empêche un client bricolé de
            // jouer les combattants des autres, y compris pendant son propre tour.
            if (!Owns(playerId, command.FighterId))
            {
                Reject(playerId, CommandError.NotYourFighter);
                return;
            }

            CommandResult result = Battle.Execute(command);
            if (!result.Success)
            {
                Reject(playerId, result.Error);
                return;
            }

            _transport.Broadcast(BattleCodec.EncodeEvents(result.Events));
        }

        private void Reject(int playerId, CommandError error)
        {
            _transport.Send(playerId, BattleCodec.EncodeRejection(error));
            CommandRejected?.Invoke(playerId, error);
        }
    }
}
