using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Raison du refus d'une commande. Le serveur la renvoie telle quelle au
    /// client, qui sait ainsi quoi afficher sans rejouer la règle de son côté.
    /// </summary>
    public enum CommandError
    {
        None = 0,
        BattleNotStarted,
        BattleOver,
        UnknownFighter,
        NotYourTurn,
        FighterDead,
        DestinationOutOfBounds,
        DestinationNotWalkable,
        DestinationOccupied,
        Unreachable,
        UnknownSpell,
        NotEnoughActionPoints,
        OutOfRange,
        NoLineOfSight,
        SpellOnCooldown,
        CastLimitReached,
        NoTargetAtCell,
    }

    /// <summary>Résultat de l'exécution d'une commande : les faits, ou le refus.</summary>
    public sealed class CommandResult
    {
        private static readonly BattleEvent[] NoEvents = new BattleEvent[0];

        public bool Success => Error == CommandError.None;

        public CommandError Error { get; }

        public IReadOnlyList<BattleEvent> Events { get; }

        private CommandResult(CommandError error, IReadOnlyList<BattleEvent> events)
        {
            Error = error;
            Events = events;
        }

        public static CommandResult Ok(IReadOnlyList<BattleEvent> events)
        {
            return new CommandResult(CommandError.None, events ?? NoEvents);
        }

        public static CommandResult Fail(CommandError error)
        {
            return new CommandResult(error, NoEvents);
        }
    }
}
