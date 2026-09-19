using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dovaky.Combat.Net
{
    public enum MessageType : byte
    {
        Command = 1,
        Events = 2,
        CommandRejected = 3,
        Snapshot = 4,
    }

    /// <summary>
    /// Sérialisation binaire du protocole de combat. Le format est explicite
    /// et versionné par le premier octet de chaque message : un client d'une
    /// autre version échoue proprement au lieu de mal interpréter les octets.
    ///
    /// Le décodage ne fait aucune confiance à son entrée — elle vient du
    /// réseau. Un message tronqué ou d'un type inconnu lève une
    /// <see cref="ProtocolException"/>, que l'appelant traite comme une
    /// déconnexion plutôt que comme un plantage.
    /// </summary>
    public static class BattleCodec
    {
        private const byte MoveCommandTag = 1;
        private const byte CastCommandTag = 2;
        private const byte EndTurnCommandTag = 3;

        private const byte TurnStartedTag = 1;
        private const byte TurnEndedTag = 2;
        private const byte FighterMovedTag = 3;
        private const byte SpellCastTag = 4;
        private const byte DamageTakenTag = 5;
        private const byte FighterDiedTag = 6;
        private const byte BattleEndedTag = 7;

        public static MessageType PeekType(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new ProtocolException("Message vide.");

            byte type = payload[0];
            if (type < (byte)MessageType.Command || type > (byte)MessageType.Snapshot)
                throw new ProtocolException("Type de message inconnu : " + type);

            return (MessageType)type;
        }

        public static byte[] EncodeCommand(BattleCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)MessageType.Command);

                if (command is MoveCommand move)
                {
                    writer.Write(MoveCommandTag);
                    writer.Write(move.FighterId);
                    WriteCell(writer, move.Destination);
                }
                else if (command is CastSpellCommand cast)
                {
                    writer.Write(CastCommandTag);
                    writer.Write(cast.FighterId);
                    writer.Write(cast.SpellId);
                    WriteCell(writer, cast.Target);
                }
                else if (command is EndTurnCommand)
                {
                    writer.Write(EndTurnCommandTag);
                    writer.Write(command.FighterId);
                }
                else
                {
                    throw new ArgumentException("Commande non sérialisable : " + command.GetType().Name, nameof(command));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static BattleCommand DecodeCommandCore(byte[] payload)
        {
            using (BinaryReader reader = OpenReader(payload, MessageType.Command))
            {
                byte tag = reader.ReadByte();
                switch (tag)
                {
                    case MoveCommandTag:
                        return new MoveCommand(reader.ReadInt32(), ReadCell(reader));
                    case CastCommandTag:
                        return new CastSpellCommand(reader.ReadInt32(), reader.ReadInt32(), ReadCell(reader));
                    case EndTurnCommandTag:
                        return new EndTurnCommand(reader.ReadInt32());
                    default:
                        throw new ProtocolException("Commande inconnue : " + tag);
                }
            }
        }

        public static byte[] EncodeEvents(IReadOnlyList<BattleEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)MessageType.Events);
                writer.Write(events.Count);

                for (int i = 0; i < events.Count; i++) WriteEvent(writer, events[i]);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static List<BattleEvent> DecodeEventsCore(byte[] payload)
        {
            using (BinaryReader reader = OpenReader(payload, MessageType.Events))
            {
                int count = reader.ReadInt32();
                if (count < 0) throw new ProtocolException("Nombre d'événements négatif.");

                var events = new List<BattleEvent>(count);
                for (int i = 0; i < count; i++) events.Add(ReadEvent(reader));
                return events;
            }
        }

        public static byte[] EncodeRejection(CommandError error)
        {
            return new[] { (byte)MessageType.CommandRejected, (byte)error };
        }

        private static CommandError DecodeRejectionCore(byte[] payload)
        {
            using (BinaryReader reader = OpenReader(payload, MessageType.CommandRejected))
            {
                byte value = reader.ReadByte();
                if (!Enum.IsDefined(typeof(CommandError), (int)value))
                    throw new ProtocolException("Code de refus inconnu : " + value);

                return (CommandError)value;
            }
        }

        public static byte[] EncodeSnapshot(BattleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)MessageType.Snapshot);
                writer.Write(snapshot.Width);
                writer.Write(snapshot.Height);
                writer.Write(snapshot.Cells.Length);
                writer.Write(snapshot.Cells);
                writer.Write(snapshot.Round);
                writer.Write(snapshot.ActiveFighterId);
                writer.Write(snapshot.IsOver);
                writer.Write(snapshot.WinningTeamId.HasValue);
                writer.Write(snapshot.WinningTeamId ?? 0);

                writer.Write(snapshot.Fighters.Count);
                foreach (FighterSnapshot fighter in snapshot.Fighters)
                {
                    writer.Write(fighter.Id);
                    writer.Write(fighter.Name ?? string.Empty);
                    writer.Write(fighter.TeamId);
                    WriteCell(writer, fighter.Position);
                    writer.Write(fighter.Health);
                    writer.Write(fighter.MaxHealth);
                    writer.Write(fighter.ActionPoints);
                    writer.Write(fighter.MaxActionPoints);
                    writer.Write(fighter.MovementPoints);
                    writer.Write(fighter.MaxMovementPoints);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static BattleSnapshot DecodeSnapshotCore(byte[] payload)
        {
            using (BinaryReader reader = OpenReader(payload, MessageType.Snapshot))
            {
                var snapshot = new BattleSnapshot
                {
                    Width = reader.ReadInt32(),
                    Height = reader.ReadInt32(),
                };

                int cellCount = reader.ReadInt32();
                if (cellCount < 0 || cellCount != snapshot.Width * snapshot.Height)
                    throw new ProtocolException("Taille de carte incohérente.");

                snapshot.Cells = reader.ReadBytes(cellCount);
                if (snapshot.Cells.Length != cellCount) throw new ProtocolException("Carte tronquée.");

                snapshot.Round = reader.ReadInt32();
                snapshot.ActiveFighterId = reader.ReadInt32();
                snapshot.IsOver = reader.ReadBoolean();

                bool hasWinner = reader.ReadBoolean();
                int winner = reader.ReadInt32();
                snapshot.WinningTeamId = hasWinner ? winner : (int?)null;

                int fighterCount = reader.ReadInt32();
                if (fighterCount < 0) throw new ProtocolException("Nombre de combattants négatif.");

                for (int i = 0; i < fighterCount; i++)
                {
                    snapshot.Fighters.Add(new FighterSnapshot
                    {
                        Id = reader.ReadInt32(),
                        Name = reader.ReadString(),
                        TeamId = reader.ReadInt32(),
                        Position = ReadCell(reader),
                        Health = reader.ReadInt32(),
                        MaxHealth = reader.ReadInt32(),
                        ActionPoints = reader.ReadInt32(),
                        MaxActionPoints = reader.ReadInt32(),
                        MovementPoints = reader.ReadInt32(),
                        MaxMovementPoints = reader.ReadInt32(),
                    });
                }

                return snapshot;
            }
        }

        /// <summary>
        /// Décode en neutralisant les entrées malformées : un message tronqué
        /// ou incohérent ressort en <see cref="ProtocolException"/>, jamais en
        /// exception de bas niveau que l'appelant ne saurait pas interpréter.
        /// </summary>
        public static BattleCommand DecodeCommand(byte[] payload) => Guarded(() => DecodeCommandCore(payload));

        public static List<BattleEvent> DecodeEvents(byte[] payload) => Guarded(() => DecodeEventsCore(payload));

        public static CommandError DecodeRejection(byte[] payload) => Guarded(() => DecodeRejectionCore(payload));

        public static BattleSnapshot DecodeSnapshot(byte[] payload) => Guarded(() => DecodeSnapshotCore(payload));

        private static T Guarded<T>(Func<T> decode)
        {
            try
            {
                return decode();
            }
            catch (EndOfStreamException)
            {
                throw new ProtocolException("Message tronqué.");
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolException("Message malformé : " + exception.Message);
            }
        }

        private static void WriteEvent(BinaryWriter writer, BattleEvent battleEvent)
        {
            if (battleEvent is TurnStarted turnStarted)
            {
                writer.Write(TurnStartedTag);
                writer.Write(turnStarted.FighterId);
                writer.Write(turnStarted.Round);
            }
            else if (battleEvent is TurnEnded turnEnded)
            {
                writer.Write(TurnEndedTag);
                writer.Write(turnEnded.FighterId);
            }
            else if (battleEvent is FighterMoved moved)
            {
                writer.Write(FighterMovedTag);
                writer.Write(moved.FighterId);
                writer.Write(moved.MovementPointsLeft);
                writer.Write(moved.Path.Count);
                for (int i = 0; i < moved.Path.Count; i++) WriteCell(writer, moved.Path[i]);
            }
            else if (battleEvent is SpellCast cast)
            {
                writer.Write(SpellCastTag);
                writer.Write(cast.CasterId);
                writer.Write(cast.SpellId);
                WriteCell(writer, cast.Target);
                writer.Write(cast.ActionPointsLeft);
            }
            else if (battleEvent is DamageTaken damage)
            {
                writer.Write(DamageTakenTag);
                writer.Write(damage.FighterId);
                writer.Write(damage.Amount);
                writer.Write(damage.HealthLeft);
            }
            else if (battleEvent is FighterDied died)
            {
                writer.Write(FighterDiedTag);
                writer.Write(died.FighterId);
            }
            else if (battleEvent is BattleEnded ended)
            {
                writer.Write(BattleEndedTag);
                writer.Write(ended.WinningTeamId.HasValue);
                writer.Write(ended.WinningTeamId ?? 0);
            }
            else
            {
                throw new ArgumentException("Événement non sérialisable : " + battleEvent.GetType().Name);
            }
        }

        private static BattleEvent ReadEvent(BinaryReader reader)
        {
            byte tag = reader.ReadByte();
            switch (tag)
            {
                case TurnStartedTag:
                    return new TurnStarted(reader.ReadInt32(), reader.ReadInt32());
                case TurnEndedTag:
                    return new TurnEnded(reader.ReadInt32());
                case FighterMovedTag:
                {
                    int fighterId = reader.ReadInt32();
                    int movementPointsLeft = reader.ReadInt32();
                    int length = reader.ReadInt32();
                    if (length < 0) throw new ProtocolException("Longueur de chemin négative.");

                    var path = new List<Cell>(length);
                    for (int i = 0; i < length; i++) path.Add(ReadCell(reader));
                    return new FighterMoved(fighterId, path, movementPointsLeft);
                }
                case SpellCastTag:
                    return new SpellCast(reader.ReadInt32(), reader.ReadInt32(), ReadCell(reader), reader.ReadInt32());
                case DamageTakenTag:
                    return new DamageTaken(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                case FighterDiedTag:
                    return new FighterDied(reader.ReadInt32());
                case BattleEndedTag:
                {
                    bool hasWinner = reader.ReadBoolean();
                    int winner = reader.ReadInt32();
                    return new BattleEnded(hasWinner ? winner : (int?)null);
                }
                default:
                    throw new ProtocolException("Événement inconnu : " + tag);
            }
        }

        private static BinaryReader OpenReader(byte[] payload, MessageType expected)
        {
            if (PeekType(payload) != expected)
                throw new ProtocolException("Message de type " + PeekType(payload) + ", attendu " + expected + ".");

            var reader = new BinaryReader(new MemoryStream(payload, 1, payload.Length - 1, false), Encoding.UTF8);
            return reader;
        }

        private static void WriteCell(BinaryWriter writer, Cell cell)
        {
            writer.Write(cell.X);
            writer.Write(cell.Y);
        }

        private static Cell ReadCell(BinaryReader reader) => new Cell(reader.ReadInt32(), reader.ReadInt32());
    }

    /// <summary>Message réseau inexploitable : à traiter comme un pair fautif.</summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }
}
