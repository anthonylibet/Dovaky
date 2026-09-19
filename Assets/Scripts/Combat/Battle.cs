using System;
using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Simulation d'un combat au tour par tour sur grille.
    ///
    /// C'est l'autorité de la partie : elle n'expose aucune méthode qui modifie
    /// l'état directement, seulement <see cref="Execute"/>, qui valide une
    /// commande puis en publie les conséquences sous forme d'événements. Le
    /// serveur exécute les commandes des joueurs ; les clients rejouent les
    /// événements reçus. Aucune dépendance à Unity : la même classe tourne dans
    /// l'éditeur, dans le build et dans un serveur dédié.
    ///
    /// La simulation est déterministe à graine égale : même suite de commandes,
    /// même suite d'événements.
    /// </summary>
    public sealed class Battle
    {
        private readonly List<Fighter> _fighters;
        private readonly List<Fighter> _turnOrder;
        private readonly DeterministicRandom _random;

        private int _turnIndex;

        public BattleMap Map { get; }

        public IReadOnlyList<Fighter> Fighters => _fighters;

        /// <summary>Ordre de jeu, fixé au début du combat.</summary>
        public IReadOnlyList<Fighter> TurnOrder => _turnOrder;

        public bool IsStarted { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Équipe gagnante une fois le combat terminé, null sinon (ou match nul).</summary>
        public int? WinningTeamId { get; private set; }

        /// <summary>Numéro du tour de jeu complet, à partir de 1.</summary>
        public int Round { get; private set; }

        public Fighter ActiveFighter => IsStarted && !IsOver ? _turnOrder[_turnIndex] : null;

        public Battle(BattleMap map, IEnumerable<Fighter> fighters, ulong seed)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (fighters == null) throw new ArgumentNullException(nameof(fighters));

            _fighters = new List<Fighter>(fighters);
            if (_fighters.Count == 0) throw new ArgumentException("Un combat exige au moins un combattant.", nameof(fighters));

            _random = new DeterministicRandom(seed);

            // Initiative décroissante, puis identifiant croissant : l'ordre ne
            // dépend jamais de l'ordre d'arrivée des joueurs sur le serveur.
            _turnOrder = new List<Fighter>(_fighters);
            _turnOrder.Sort((a, b) =>
            {
                int byInitiative = b.Initiative.CompareTo(a.Initiative);
                return byInitiative != 0 ? byInitiative : a.Id.CompareTo(b.Id);
            });
        }

        /// <summary>Démarre le combat et ouvre le tour du premier combattant.</summary>
        public IReadOnlyList<BattleEvent> Start()
        {
            if (IsStarted) throw new InvalidOperationException("Le combat a déjà démarré.");

            IsStarted = true;
            Round = 1;
            _turnIndex = 0;

            var events = new List<BattleEvent>();
            Fighter first = _turnOrder[_turnIndex];
            first.BeginTurn();
            events.Add(new TurnStarted(first.Id, Round));
            return events;
        }

        public Fighter FindFighter(int fighterId)
        {
            for (int i = 0; i < _fighters.Count; i++)
            {
                if (_fighters[i].Id == fighterId) return _fighters[i];
            }

            return null;
        }

        /// <summary>Combattant vivant occupant cette case, s'il y en a un.</summary>
        public Fighter FighterAt(Cell cell)
        {
            for (int i = 0; i < _fighters.Count; i++)
            {
                if (_fighters[i].IsAlive && _fighters[i].Position == cell) return _fighters[i];
            }

            return null;
        }

        /// <summary>
        /// Cases que ce combattant peut atteindre avec ses PM restants, avec
        /// leur coût — de quoi éclairer la zone de déplacement côté client.
        /// </summary>
        public Dictionary<Cell, int> ReachableCells(Fighter fighter)
        {
            if (fighter == null) throw new ArgumentNullException(nameof(fighter));
            return Pathfinder.ReachableCells(Map, fighter.Position, fighter.MovementPoints, OccupiedCells(fighter));
        }

        /// <summary>Valide une commande et, si elle est légale, l'applique.</summary>
        public CommandResult Execute(BattleCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsStarted) return CommandResult.Fail(CommandError.BattleNotStarted);
            if (IsOver) return CommandResult.Fail(CommandError.BattleOver);

            Fighter fighter = FindFighter(command.FighterId);
            if (fighter == null) return CommandResult.Fail(CommandError.UnknownFighter);
            if (!fighter.IsAlive) return CommandResult.Fail(CommandError.FighterDead);
            if (fighter != ActiveFighter) return CommandResult.Fail(CommandError.NotYourTurn);

            if (command is MoveCommand move) return ExecuteMove(fighter, move);
            if (command is CastSpellCommand cast) return ExecuteCast(fighter, cast);
            if (command is EndTurnCommand) return CommandResult.Ok(AdvanceTurn(fighter));

            throw new ArgumentException("Commande inconnue : " + command.GetType().Name, nameof(command));
        }

        private CommandResult ExecuteMove(Fighter fighter, MoveCommand command)
        {
            Cell destination = command.Destination;

            if (!Map.Contains(destination)) return CommandResult.Fail(CommandError.DestinationOutOfBounds);
            if (!Map.IsWalkable(destination)) return CommandResult.Fail(CommandError.DestinationNotWalkable);
            if (FighterAt(destination) != null) return CommandResult.Fail(CommandError.DestinationOccupied);

            List<Cell> path = Pathfinder.FindPath(
                Map,
                fighter.Position,
                destination,
                fighter.MovementPoints,
                OccupiedCells(fighter));

            if (path == null) return CommandResult.Fail(CommandError.Unreachable);

            fighter.MovementPoints -= path.Count;
            fighter.Position = destination;

            return CommandResult.Ok(new BattleEvent[]
            {
                new FighterMoved(fighter.Id, path, fighter.MovementPoints),
            });
        }

        private CommandResult ExecuteCast(Fighter fighter, CastSpellCommand command)
        {
            Spell spell = fighter.FindSpell(command.SpellId);
            if (spell == null) return CommandResult.Fail(CommandError.UnknownSpell);
            if (fighter.ActionPoints < spell.ActionPointCost) return CommandResult.Fail(CommandError.NotEnoughActionPoints);
            if (fighter.IsOnCooldown(spell, Round)) return CommandResult.Fail(CommandError.SpellOnCooldown);
            if (spell.MaxCastsPerTurn > 0 && fighter.CastsThisTurn(spell) >= spell.MaxCastsPerTurn)
            {
                return CommandResult.Fail(CommandError.CastLimitReached);
            }

            Cell target = command.Target;
            if (!Map.Contains(target)) return CommandResult.Fail(CommandError.DestinationOutOfBounds);

            int distance = Cell.Distance(fighter.Position, target);
            if (distance < spell.MinRange || distance > spell.MaxRange) return CommandResult.Fail(CommandError.OutOfRange);

            if (spell.RequiresLineOfSight &&
                !LineOfSight.HasLineOfSight(Map, fighter.Position, target, OccupiedCells(fighter)))
            {
                return CommandResult.Fail(CommandError.NoLineOfSight);
            }

            Fighter victim = FighterAt(target);
            if (victim == null) return CommandResult.Fail(CommandError.NoTargetAtCell);

            fighter.ActionPoints -= spell.ActionPointCost;
            fighter.RegisterCast(spell, Round);

            var events = new List<BattleEvent>
            {
                new SpellCast(fighter.Id, spell.Id, target, fighter.ActionPoints),
            };

            int damage = _random.NextInt(spell.MinDamage, spell.MaxDamage);
            victim.Health = Math.Max(0, victim.Health - damage);
            events.Add(new DamageTaken(victim.Id, damage, victim.Health));

            if (!victim.IsAlive)
            {
                events.Add(new FighterDied(victim.Id));

                if (CheckBattleOver(out int? winner))
                {
                    IsOver = true;
                    WinningTeamId = winner;
                    events.Add(new BattleEnded(winner));
                    return CommandResult.Ok(events);
                }

                // Un lanceur qui se tue lui-même ne peut pas finir son tour.
                if (!fighter.IsAlive) events.AddRange(AdvanceTurn(fighter));
            }

            return CommandResult.Ok(events);
        }

        /// <summary>Clôt le tour courant et ouvre celui du prochain combattant vivant.</summary>
        private List<BattleEvent> AdvanceTurn(Fighter current)
        {
            var events = new List<BattleEvent> { new TurnEnded(current.Id) };

            for (int step = 0; step < _turnOrder.Count; step++)
            {
                _turnIndex++;
                if (_turnIndex >= _turnOrder.Count)
                {
                    _turnIndex = 0;
                    Round++;
                }

                Fighter next = _turnOrder[_turnIndex];
                if (!next.IsAlive) continue;

                next.BeginTurn();
                events.Add(new TurnStarted(next.Id, Round));
                return events;
            }

            // Plus personne pour jouer : le combat s'arrête là.
            IsOver = true;
            CheckBattleOver(out int? winner);
            WinningTeamId = winner;
            events.Add(new BattleEnded(winner));
            return events;
        }

        /// <summary>Le combat est fini dès qu'il reste au plus une équipe debout.</summary>
        private bool CheckBattleOver(out int? winningTeamId)
        {
            winningTeamId = null;
            var teams = new HashSet<int>();

            for (int i = 0; i < _fighters.Count; i++)
            {
                if (_fighters[i].IsAlive) teams.Add(_fighters[i].TeamId);
            }

            if (teams.Count > 1) return false;

            foreach (int team in teams) winningTeamId = team;
            return true;
        }

        /// <summary>Cases tenues par les combattants vivants, hors celui qui agit.</summary>
        private List<Cell> OccupiedCells(Fighter excluded)
        {
            var cells = new List<Cell>();
            for (int i = 0; i < _fighters.Count; i++)
            {
                Fighter other = _fighters[i];
                if (other == excluded || !other.IsAlive) continue;
                cells.Add(other.Position);
            }

            return cells;
        }
    }
}
