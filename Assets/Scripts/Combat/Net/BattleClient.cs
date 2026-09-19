using System;
using System.Collections.Generic;

namespace Dovaky.Combat.Net
{
    /// <summary>Ce qu'un client sait d'un combattant. Reflet, jamais autorité.</summary>
    public sealed class ClientFighter
    {
        public int Id;
        public string Name;
        public int TeamId;
        public Cell Position;
        public int Health;
        public int MaxHealth;
        public int ActionPoints;
        public int MaxActionPoints;
        public int MovementPoints;
        public int MaxMovementPoints;

        public bool IsAlive => Health > 0;
    }

    /// <summary>
    /// État du combat tel que le client le connaît : une photographie initiale,
    /// puis les événements du serveur. Il ne calcule aucune règle — s'il
    /// divergeait, le serveur resterait seul juge.
    /// </summary>
    public sealed class ClientBattleState
    {
        private readonly Dictionary<int, ClientFighter> _fighters = new Dictionary<int, ClientFighter>();
        private byte[] _cells = new byte[0];

        public int Width { get; private set; }

        public int Height { get; private set; }

        public IReadOnlyDictionary<int, ClientFighter> Fighters => _fighters;

        public int Round { get; private set; }

        public int ActiveFighterId { get; private set; }

        public bool IsOver { get; private set; }

        public int? WinningTeamId { get; private set; }

        public bool Contains(Cell cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        public CellKind GetCell(Cell cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return (CellKind)_cells[cell.Y * Width + cell.X];
        }

        public ClientFighter FighterAt(Cell cell)
        {
            foreach (ClientFighter fighter in _fighters.Values)
            {
                if (fighter.IsAlive && fighter.Position == cell) return fighter;
            }

            return null;
        }

        /// <summary>Reconstruit la carte de déplacement à afficher, sans rien décider.</summary>
        public BattleMap BuildMap()
        {
            var map = new BattleMap(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    map.SetCell(new Cell(x, y), (CellKind)_cells[y * Width + x]);
                }
            }

            return map;
        }

        internal void ApplySnapshot(BattleSnapshot snapshot)
        {
            Width = snapshot.Width;
            Height = snapshot.Height;
            _cells = snapshot.Cells;
            Round = snapshot.Round;
            ActiveFighterId = snapshot.ActiveFighterId;
            IsOver = snapshot.IsOver;
            WinningTeamId = snapshot.WinningTeamId;

            _fighters.Clear();
            foreach (FighterSnapshot fighter in snapshot.Fighters)
            {
                _fighters[fighter.Id] = new ClientFighter
                {
                    Id = fighter.Id,
                    Name = fighter.Name,
                    TeamId = fighter.TeamId,
                    Position = fighter.Position,
                    Health = fighter.Health,
                    MaxHealth = fighter.MaxHealth,
                    ActionPoints = fighter.ActionPoints,
                    MaxActionPoints = fighter.MaxActionPoints,
                    MovementPoints = fighter.MovementPoints,
                    MaxMovementPoints = fighter.MaxMovementPoints,
                };
            }
        }

        internal void Apply(BattleEvent battleEvent)
        {
            if (battleEvent is TurnStarted turnStarted)
            {
                ActiveFighterId = turnStarted.FighterId;
                Round = turnStarted.Round;
                if (_fighters.TryGetValue(turnStarted.FighterId, out ClientFighter fighter))
                {
                    fighter.ActionPoints = fighter.MaxActionPoints;
                    fighter.MovementPoints = fighter.MaxMovementPoints;
                }
            }
            else if (battleEvent is FighterMoved moved)
            {
                if (_fighters.TryGetValue(moved.FighterId, out ClientFighter fighter))
                {
                    if (moved.Path.Count > 0) fighter.Position = moved.Path[moved.Path.Count - 1];
                    fighter.MovementPoints = moved.MovementPointsLeft;
                }
            }
            else if (battleEvent is SpellCast cast)
            {
                if (_fighters.TryGetValue(cast.CasterId, out ClientFighter caster))
                {
                    caster.ActionPoints = cast.ActionPointsLeft;
                }
            }
            else if (battleEvent is DamageTaken damage)
            {
                if (_fighters.TryGetValue(damage.FighterId, out ClientFighter fighter))
                {
                    fighter.Health = damage.HealthLeft;
                }
            }
            else if (battleEvent is BattleEnded ended)
            {
                IsOver = true;
                WinningTeamId = ended.WinningTeamId;
            }
        }
    }

    /// <summary>
    /// Côté joueur : envoie des intentions, reçoit des faits. Les événements
    /// sont republiés tels quels pour que la couche d'affichage les anime,
    /// après que l'état miroir a été mis à jour.
    /// </summary>
    public sealed class BattleClient
    {
        private readonly IClientTransport _transport;

        public int PlayerId { get; }

        public ClientBattleState State { get; } = new ClientBattleState();

        /// <summary>Événement reçu du serveur, l'état miroir étant déjà à jour.</summary>
        public event Action<BattleEvent> EventReceived;

        /// <summary>Le serveur a refusé notre dernière commande.</summary>
        public event Action<CommandError> CommandRejected;

        public event Action SnapshotApplied;

        /// <summary>Message du serveur inexploitable : versions incompatibles.</summary>
        public event Action<ProtocolException> ProtocolViolation;

        public BattleClient(IClientTransport transport, int playerId)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            PlayerId = playerId;
            _transport.MessageReceived += OnMessageReceived;
        }

        public void Send(BattleCommand command) => _transport.Send(BattleCodec.EncodeCommand(command));

        private void OnMessageReceived(byte[] payload)
        {
            try
            {
                switch (BattleCodec.PeekType(payload))
                {
                    case MessageType.Snapshot:
                        State.ApplySnapshot(BattleCodec.DecodeSnapshot(payload));
                        SnapshotApplied?.Invoke();
                        break;

                    case MessageType.Events:
                        foreach (BattleEvent battleEvent in BattleCodec.DecodeEvents(payload))
                        {
                            State.Apply(battleEvent);
                            EventReceived?.Invoke(battleEvent);
                        }

                        break;

                    case MessageType.CommandRejected:
                        CommandRejected?.Invoke(BattleCodec.DecodeRejection(payload));
                        break;

                    default:
                        throw new ProtocolException("Un serveur n'envoie pas de commandes.");
                }
            }
            catch (ProtocolException exception)
            {
                ProtocolViolation?.Invoke(exception);
            }
        }
    }
}
