using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Fait accompli dans le combat. Le serveur produit ces événements en
    /// appliquant les commandes ; les clients les rejouent pour animer la scène.
    /// Un événement décrit un résultat, jamais une intention.
    /// </summary>
    public abstract class BattleEvent
    {
    }

    public sealed class TurnStarted : BattleEvent
    {
        public int FighterId { get; }

        public int Round { get; }

        public TurnStarted(int fighterId, int round)
        {
            FighterId = fighterId;
            Round = round;
        }
    }

    public sealed class TurnEnded : BattleEvent
    {
        public int FighterId { get; }

        public TurnEnded(int fighterId)
        {
            FighterId = fighterId;
        }
    }

    public sealed class FighterMoved : BattleEvent
    {
        public int FighterId { get; }

        /// <summary>Cases traversées, case de départ exclue.</summary>
        public IReadOnlyList<Cell> Path { get; }

        public int MovementPointsLeft { get; }

        public FighterMoved(int fighterId, IReadOnlyList<Cell> path, int movementPointsLeft)
        {
            FighterId = fighterId;
            Path = path;
            MovementPointsLeft = movementPointsLeft;
        }
    }

    public sealed class SpellCast : BattleEvent
    {
        public int CasterId { get; }

        public int SpellId { get; }

        public Cell Target { get; }

        public int ActionPointsLeft { get; }

        public SpellCast(int casterId, int spellId, Cell target, int actionPointsLeft)
        {
            CasterId = casterId;
            SpellId = spellId;
            Target = target;
            ActionPointsLeft = actionPointsLeft;
        }
    }

    public sealed class DamageTaken : BattleEvent
    {
        public int FighterId { get; }

        public int Amount { get; }

        public int HealthLeft { get; }

        public DamageTaken(int fighterId, int amount, int healthLeft)
        {
            FighterId = fighterId;
            Amount = amount;
            HealthLeft = healthLeft;
        }
    }

    public sealed class FighterDied : BattleEvent
    {
        public int FighterId { get; }

        public FighterDied(int fighterId)
        {
            FighterId = fighterId;
        }
    }

    public sealed class BattleEnded : BattleEvent
    {
        /// <summary>Équipe victorieuse, ou null si plus personne n'est debout.</summary>
        public int? WinningTeamId { get; }

        public BattleEnded(int? winningTeamId)
        {
            WinningTeamId = winningTeamId;
        }
    }
}
