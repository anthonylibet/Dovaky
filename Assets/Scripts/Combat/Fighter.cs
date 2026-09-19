using System;
using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Un combattant engagé dans un combat. Son état mutable (position, points,
    /// relances) n'est modifié que par <see cref="Battle"/> : côté serveur c'est
    /// la seule autorité, côté client ces valeurs ne sont qu'un reflet.
    /// </summary>
    public sealed class Fighter
    {
        private readonly List<Spell> _spells = new List<Spell>();
        private readonly Dictionary<int, int> _spellAvailableOnRound = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _castsThisTurn = new Dictionary<int, int>();

        public int Id { get; }

        public string Name { get; }

        public int TeamId { get; }

        public int MaxHealth { get; }

        public int Health { get; internal set; }

        /// <summary>PA maximum, restaurés en début de tour.</summary>
        public int MaxActionPoints { get; }

        public int ActionPoints { get; internal set; }

        /// <summary>PM maximum, restaurés en début de tour.</summary>
        public int MaxMovementPoints { get; }

        public int MovementPoints { get; internal set; }

        /// <summary>Départage l'ordre du tour, du plus élevé au plus faible.</summary>
        public int Initiative { get; }

        public Cell Position { get; internal set; }

        public bool IsAlive => Health > 0;

        public IReadOnlyList<Spell> Spells => _spells;

        public Fighter(
            int id,
            string name,
            int teamId,
            Cell position,
            int maxHealth,
            int maxActionPoints,
            int maxMovementPoints,
            int initiative)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (maxActionPoints < 0) throw new ArgumentOutOfRangeException(nameof(maxActionPoints));
            if (maxMovementPoints < 0) throw new ArgumentOutOfRangeException(nameof(maxMovementPoints));

            Id = id;
            Name = name;
            TeamId = teamId;
            Position = position;
            MaxHealth = maxHealth;
            Health = maxHealth;
            MaxActionPoints = maxActionPoints;
            ActionPoints = maxActionPoints;
            MaxMovementPoints = maxMovementPoints;
            MovementPoints = maxMovementPoints;
            Initiative = initiative;
        }

        public Fighter WithSpell(Spell spell)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            _spells.Add(spell);
            return this;
        }

        public Spell FindSpell(int spellId)
        {
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].Id == spellId) return _spells[i];
            }

            return null;
        }

        internal bool IsOnCooldown(Spell spell, int currentRound)
        {
            return _spellAvailableOnRound.TryGetValue(spell.Id, out int availableOn) && currentRound < availableOn;
        }

        internal int CastsThisTurn(Spell spell)
        {
            return _castsThisTurn.TryGetValue(spell.Id, out int casts) ? casts : 0;
        }

        internal void RegisterCast(Spell spell, int currentRound)
        {
            _castsThisTurn[spell.Id] = CastsThisTurn(spell) + 1;
            if (spell.CooldownTurns > 0)
            {
                _spellAvailableOnRound[spell.Id] = currentRound + spell.CooldownTurns;
            }
        }

        /// <summary>Restaure PA/PM et remet à zéro les lancers du tour.</summary>
        internal void BeginTurn()
        {
            ActionPoints = MaxActionPoints;
            MovementPoints = MaxMovementPoints;
            _castsThisTurn.Clear();
        }
    }
}
