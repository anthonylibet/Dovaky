using System.Collections.Generic;
using Dovaky.Combat;
using NUnit.Framework;

namespace Dovaky.Combat.Tests
{
    [TestFixture]
    public class BattleTests
    {
        private const int DagueId = 1;

        private Battle _battle;
        private Fighter _alice;
        private Fighter _bob;

        /// <summary>
        /// Terrain dégagé 7x7, Alice en (0,0) face à Bob en (3,0).
        /// La dague inflige exactement 5 dégâts : les assertions restent lisibles.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _battle = BuildBattle(pointsDeVieBob: 30, degatsMin: 5, degatsMax: 5);
        }

        private Battle BuildBattle(
            int pointsDeVieBob,
            int degatsMin,
            int degatsMax,
            int cooldownTurns = 0,
            int maxCastsPerTurn = 0,
            ulong seed = 42UL,
            BattleMap map = null)
        {
            var dague = new Spell(
                DagueId,
                "Dague",
                actionPointCost: 3,
                minRange: 1,
                maxRange: 3,
                minDamage: degatsMin,
                maxDamage: degatsMax,
                requiresLineOfSight: true,
                cooldownTurns: cooldownTurns,
                maxCastsPerTurn: maxCastsPerTurn);

            _alice = new Fighter(1, "Alice", teamId: 0, position: new Cell(0, 0),
                maxHealth: 50, maxActionPoints: 6, maxMovementPoints: 3, initiative: 10).WithSpell(dague);
            _bob = new Fighter(2, "Bob", teamId: 1, position: new Cell(3, 0),
                maxHealth: pointsDeVieBob, maxActionPoints: 6, maxMovementPoints: 3, initiative: 5);

            BattleMap terrain = map ?? BattleMap.FromRows(
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......");

            var battle = new Battle(terrain, new[] { _bob, _alice }, seed);
            battle.Start();
            return battle;
        }

        [Test]
        public void OrdreDuTour_TrieParInitiativeDecroissante()
        {
            Assert.AreEqual(2, _battle.TurnOrder.Count);
            Assert.AreSame(_alice, _battle.TurnOrder[0]);
            Assert.AreSame(_bob, _battle.TurnOrder[1]);
            Assert.AreSame(_alice, _battle.ActiveFighter);
        }

        [Test]
        public void Start_OuvreLePremierTour()
        {
            var battle = BuildBattle(30, 5, 5);
            // Start() a déjà été appelé par BuildBattle : on vérifie l'état résultant.
            Assert.IsTrue(battle.IsStarted);
            Assert.AreEqual(1, battle.Round);
            Assert.AreEqual(3, battle.ActiveFighter.MovementPoints);
            Assert.AreEqual(6, battle.ActiveFighter.ActionPoints);
        }

        [Test]
        public void Deplacement_ConsommeLesPmEtDeplaceLeCombattant()
        {
            CommandResult resultat = _battle.Execute(new MoveCommand(_alice.Id, new Cell(2, 0)));

            Assert.IsTrue(resultat.Success);
            Assert.AreEqual(new Cell(2, 0), _alice.Position);
            Assert.AreEqual(1, _alice.MovementPoints);

            var deplacement = (FighterMoved)resultat.Events[0];
            Assert.AreEqual(2, deplacement.Path.Count);
            Assert.AreEqual(1, deplacement.MovementPointsLeft);
        }

        [Test]
        public void Deplacement_SurUneCaseOccupee_EstRefuse()
        {
            CommandResult resultat = _battle.Execute(new MoveCommand(_alice.Id, new Cell(3, 0)));

            Assert.AreEqual(CommandError.DestinationOccupied, resultat.Error);
            Assert.AreEqual(new Cell(0, 0), _alice.Position);
        }

        [Test]
        public void Deplacement_HorsPortee_EstRefuse()
        {
            CommandResult resultat = _battle.Execute(new MoveCommand(_alice.Id, new Cell(0, 5)));

            Assert.AreEqual(CommandError.Unreachable, resultat.Error);
            Assert.AreEqual(3, _alice.MovementPoints);
        }

        [Test]
        public void Deplacement_HorsCarte_EstRefuse()
        {
            CommandResult resultat = _battle.Execute(new MoveCommand(_alice.Id, new Cell(-1, 0)));

            Assert.AreEqual(CommandError.DestinationOutOfBounds, resultat.Error);
        }

        [Test]
        public void UneCommandeHorsTour_EstRefusee()
        {
            CommandResult resultat = _battle.Execute(new MoveCommand(_bob.Id, new Cell(3, 1)));

            Assert.AreEqual(CommandError.NotYourTurn, resultat.Error);
            Assert.AreEqual(new Cell(3, 0), _bob.Position);
        }

        [Test]
        public void UnCombattantInconnu_EstRefuse()
        {
            Assert.AreEqual(CommandError.UnknownFighter, _battle.Execute(new EndTurnCommand(99)).Error);
        }

        [Test]
        public void Sort_InfligeDesDegatsEtCoutteDesPa()
        {
            CommandResult resultat = _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.IsTrue(resultat.Success);
            Assert.AreEqual(25, _bob.Health);
            Assert.AreEqual(3, _alice.ActionPoints);

            var lancer = (SpellCast)resultat.Events[0];
            Assert.AreEqual(DagueId, lancer.SpellId);
            var degats = (DamageTaken)resultat.Events[1];
            Assert.AreEqual(5, degats.Amount);
            Assert.AreEqual(25, degats.HealthLeft);
        }

        [Test]
        public void Sort_HorsPortee_EstRefuse()
        {
            CommandResult resultat = _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, new Cell(6, 0)));

            Assert.AreEqual(CommandError.OutOfRange, resultat.Error);
            Assert.AreEqual(6, _alice.ActionPoints);
        }

        [Test]
        public void Sort_SurUneCaseVide_EstRefuse()
        {
            CommandResult resultat = _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, new Cell(2, 0)));

            Assert.AreEqual(CommandError.NoTargetAtCell, resultat.Error);
            Assert.AreEqual(6, _alice.ActionPoints);
        }

        [Test]
        public void Sort_Inconnu_EstRefuse()
        {
            Assert.AreEqual(
                CommandError.UnknownSpell,
                _battle.Execute(new CastSpellCommand(_alice.Id, 999, _bob.Position)).Error);
        }

        [Test]
        public void Sort_SansAssezDePa_EstRefuse()
        {
            _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));
            _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            CommandResult troisieme = _battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.AreEqual(CommandError.NotEnoughActionPoints, troisieme.Error);
            Assert.AreEqual(0, _alice.ActionPoints);
            Assert.AreEqual(20, _bob.Health);
        }

        [Test]
        public void Sort_SansLigneDeVue_EstRefuse()
        {
            BattleMap terrain = BattleMap.FromRows(
                ".#.....",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......");
            Battle battle = BuildBattle(30, 5, 5, map: terrain);

            // Bob est en (3,0), le mur en (1,0) coupe la ligne.
            CommandResult resultat = battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.AreEqual(CommandError.NoLineOfSight, resultat.Error);
        }

        [Test]
        public void Sort_EnRelance_EstRefuseAuTourSuivant()
        {
            Battle battle = BuildBattle(30, 5, 5, cooldownTurns: 2);

            Assert.IsTrue(battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position)).Success);
            battle.Execute(new EndTurnCommand(_alice.Id));
            battle.Execute(new EndTurnCommand(_bob.Id));

            Assert.AreEqual(2, battle.Round);
            Assert.AreEqual(
                CommandError.SpellOnCooldown,
                battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position)).Error);
        }

        [Test]
        public void Sort_LimiteDeLancersParTour_EstRespectee()
        {
            Battle battle = BuildBattle(30, 5, 5, maxCastsPerTurn: 1);

            Assert.IsTrue(battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position)).Success);
            CommandResult second = battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.AreEqual(CommandError.CastLimitReached, second.Error);
            Assert.AreEqual(3, _alice.ActionPoints);
        }

        [Test]
        public void FinDeTour_PasseAuSuivantPuisIncrementeLaManche()
        {
            _battle.Execute(new MoveCommand(_alice.Id, new Cell(1, 0)));
            CommandResult finAlice = _battle.Execute(new EndTurnCommand(_alice.Id));

            Assert.IsTrue(finAlice.Success);
            Assert.AreSame(_bob, _battle.ActiveFighter);
            Assert.AreEqual(1, _battle.Round);

            _battle.Execute(new EndTurnCommand(_bob.Id));

            Assert.AreSame(_alice, _battle.ActiveFighter);
            Assert.AreEqual(2, _battle.Round);
            Assert.AreEqual(3, _alice.MovementPoints);
            Assert.AreEqual(6, _alice.ActionPoints);
        }

        [Test]
        public void MortDuDernierAdversaire_TermineLeCombat()
        {
            Battle battle = BuildBattle(pointsDeVieBob: 10, degatsMin: 5, degatsMax: 5);

            battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));
            CommandResult fatal = battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.IsTrue(fatal.Success);
            Assert.IsFalse(_bob.IsAlive);
            Assert.IsTrue(battle.IsOver);
            Assert.AreEqual(0, battle.WinningTeamId);

            var evenements = new List<BattleEvent>(fatal.Events);
            Assert.IsTrue(evenements[evenements.Count - 2] is FighterDied);
            var fin = (BattleEnded)evenements[evenements.Count - 1];
            Assert.AreEqual(0, fin.WinningTeamId);
        }

        [Test]
        public void ApresLaFinDuCombat_ToutesLesCommandesSontRefusees()
        {
            Battle battle = BuildBattle(pointsDeVieBob: 5, degatsMin: 5, degatsMax: 5);
            battle.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.IsTrue(battle.IsOver);
            Assert.AreEqual(
                CommandError.BattleOver,
                battle.Execute(new MoveCommand(_alice.Id, new Cell(1, 0))).Error);
        }

        [Test]
        public void AGraineEgale_LesDegatsAleatoiresSontIdentiques()
        {
            var premiers = new List<int>();
            var seconds = new List<int>();

            Battle premierCombat = BuildBattle(200, 10, 40, seed: 12345UL);
            premiers.Add(Degats(premierCombat.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position))));
            premiers.Add(Degats(premierCombat.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position))));

            Battle secondCombat = BuildBattle(200, 10, 40, seed: 12345UL);
            seconds.Add(Degats(secondCombat.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position))));
            seconds.Add(Degats(secondCombat.Execute(new CastSpellCommand(_alice.Id, DagueId, _bob.Position))));

            Assert.AreEqual(premiers[0], seconds[0]);
            Assert.AreEqual(premiers[1], seconds[1]);
            Assert.IsTrue(premiers[0] >= 10 && premiers[0] <= 40);
        }

        [Test]
        public void ZoneDeDeplacement_ExclutLesCasesTenuesParLesAutres()
        {
            var atteignables = _battle.ReachableCells(_alice);

            Assert.IsFalse(atteignables.ContainsKey(_bob.Position));
            Assert.IsTrue(atteignables.ContainsKey(new Cell(2, 0)));
        }

        private static int Degats(CommandResult resultat)
        {
            foreach (BattleEvent evenement in resultat.Events)
            {
                if (evenement is DamageTaken degats) return degats.Amount;
            }

            return -1;
        }
    }
}
