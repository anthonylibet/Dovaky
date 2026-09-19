namespace Dovaky.Combat
{
    /// <summary>
    /// Intention d'un joueur. Une commande arrive du réseau et n'est jamais
    /// digne de confiance : <see cref="Battle.Execute"/> la valide entièrement
    /// avant d'en tirer le moindre effet.
    /// </summary>
    public abstract class BattleCommand
    {
        /// <summary>Combattant censé agir. Le serveur vérifie que c'est bien son tour.</summary>
        public int FighterId { get; }

        protected BattleCommand(int fighterId)
        {
            FighterId = fighterId;
        }
    }

    public sealed class MoveCommand : BattleCommand
    {
        public Cell Destination { get; }

        public MoveCommand(int fighterId, Cell destination) : base(fighterId)
        {
            Destination = destination;
        }
    }

    public sealed class CastSpellCommand : BattleCommand
    {
        public int SpellId { get; }

        public Cell Target { get; }

        public CastSpellCommand(int fighterId, int spellId, Cell target) : base(fighterId)
        {
            SpellId = spellId;
            Target = target;
        }
    }

    public sealed class EndTurnCommand : BattleCommand
    {
        public EndTurnCommand(int fighterId) : base(fighterId)
        {
        }
    }
}
