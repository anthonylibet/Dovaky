using System;

namespace Dovaky.Combat
{
    /// <summary>
    /// Définition d'un sort. Immuable : c'est une donnée de référence partagée
    /// par tous les combattants qui le connaissent, jamais un état de partie.
    /// </summary>
    public sealed class Spell
    {
        public int Id { get; }

        public string Name { get; }

        /// <summary>Coût en points d'action (PA).</summary>
        public int ActionPointCost { get; }

        public int MinRange { get; }

        public int MaxRange { get; }

        /// <summary>Si vrai, la cible doit être en ligne de vue depuis le lanceur.</summary>
        public bool RequiresLineOfSight { get; }

        public int MinDamage { get; }

        public int MaxDamage { get; }

        /// <summary>Nombre de tours avant de pouvoir relancer le sort. 0 = pas de relance.</summary>
        public int CooldownTurns { get; }

        /// <summary>Nombre de lancers autorisés par tour. 0 = illimité.</summary>
        public int MaxCastsPerTurn { get; }

        public Spell(
            int id,
            string name,
            int actionPointCost,
            int minRange,
            int maxRange,
            int minDamage,
            int maxDamage,
            bool requiresLineOfSight = true,
            int cooldownTurns = 0,
            int maxCastsPerTurn = 0)
        {
            if (actionPointCost < 0) throw new ArgumentOutOfRangeException(nameof(actionPointCost));
            if (minRange < 0) throw new ArgumentOutOfRangeException(nameof(minRange));
            if (maxRange < minRange) throw new ArgumentOutOfRangeException(nameof(maxRange));
            if (minDamage < 0) throw new ArgumentOutOfRangeException(nameof(minDamage));
            if (maxDamage < minDamage) throw new ArgumentOutOfRangeException(nameof(maxDamage));

            Id = id;
            Name = name;
            ActionPointCost = actionPointCost;
            MinRange = minRange;
            MaxRange = maxRange;
            MinDamage = minDamage;
            MaxDamage = maxDamage;
            RequiresLineOfSight = requiresLineOfSight;
            CooldownTurns = cooldownTurns;
            MaxCastsPerTurn = maxCastsPerTurn;
        }
    }
}
