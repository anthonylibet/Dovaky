using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using NUnit.Framework;

namespace Dovaky.Combat.Tests
{
    [TestFixture]
    public class NetworkTests
    {
        private const int DagueId = 1;
        private const int JoueurAlice = 100;
        private const int JoueurBob = 200;

        private Battle _battle;
        private Fighter _alice;
        private Fighter _bob;
        private BattleServer _serveur;
        private BattleClient _clientAlice;
        private BattleClient _clientBob;

        [SetUp]
        public void SetUp()
        {
            var dague = new Spell(DagueId, "Dague", actionPointCost: 3, minRange: 1, maxRange: 3,
                minDamage: 5, maxDamage: 5);

            _alice = new Fighter(1, "Alice", teamId: 0, position: new Cell(0, 0),
                maxHealth: 50, maxActionPoints: 6, maxMovementPoints: 3, initiative: 10).WithSpell(dague);
            _bob = new Fighter(2, "Bob", teamId: 1, position: new Cell(3, 0),
                maxHealth: 30, maxActionPoints: 6, maxMovementPoints: 3, initiative: 5).WithSpell(dague);

            BattleMap terrain = BattleMap.FromRows(
                ".......",
                ".......",
                "..#....",
                ".......",
                ".......");

            _battle = new Battle(terrain, new[] { _alice, _bob }, seed: 7UL);

            var reseau = new LoopbackNetwork();
            _clientAlice = new BattleClient(reseau.ConnectClient(JoueurAlice), JoueurAlice);
            _clientBob = new BattleClient(reseau.ConnectClient(JoueurBob), JoueurBob);

            var proprietes = new Dictionary<int, IReadOnlyList<int>>
            {
                { JoueurAlice, new[] { _alice.Id } },
                { JoueurBob, new[] { _bob.Id } },
            };

            _serveur = new BattleServer(_battle, reseau.Server, proprietes);
            _serveur.Start();
        }

        [Test]
        public void ALaConnexion_LeClientRecoitLaCarteEtLesCombattants()
        {
            ClientBattleState etat = _clientAlice.State;

            Assert.AreEqual(7, etat.Width);
            Assert.AreEqual(5, etat.Height);
            Assert.AreEqual(CellKind.Wall, etat.GetCell(new Cell(2, 2)));
            Assert.AreEqual(CellKind.Floor, etat.GetCell(new Cell(0, 0)));
            Assert.AreEqual(2, etat.Fighters.Count);
            Assert.AreEqual(new Cell(3, 0), etat.Fighters[_bob.Id].Position);
            Assert.AreEqual(_alice.Id, etat.ActiveFighterId);
            Assert.AreEqual(1, etat.Round);
        }

        [Test]
        public void UnDeplacementValide_EstRepercuteChezTousLesClients()
        {
            _clientAlice.Send(new MoveCommand(_alice.Id, new Cell(2, 0)));

            Assert.AreEqual(new Cell(2, 0), _alice.Position);
            Assert.AreEqual(new Cell(2, 0), _clientAlice.State.Fighters[_alice.Id].Position);
            Assert.AreEqual(new Cell(2, 0), _clientBob.State.Fighters[_alice.Id].Position);
            Assert.AreEqual(1, _clientBob.State.Fighters[_alice.Id].MovementPoints);
        }

        [Test]
        public void UnJoueurNePeutPasAgirAvecLeCombattantDUnAutre()
        {
            var refus = new List<CommandError>();
            _clientBob.CommandRejected += erreur => refus.Add(erreur);

            // Bob tente de déplacer le combattant d'Alice, pendant le tour d'Alice.
            _clientBob.Send(new MoveCommand(_alice.Id, new Cell(2, 0)));

            Assert.AreEqual(1, refus.Count);
            Assert.AreEqual(CommandError.NotYourFighter, refus[0]);
            Assert.AreEqual(new Cell(0, 0), _alice.Position);
        }

        [Test]
        public void HorsDeSonTour_LaCommandeEstRefusee()
        {
            var refus = new List<CommandError>();
            _clientBob.CommandRejected += erreur => refus.Add(erreur);

            _clientBob.Send(new MoveCommand(_bob.Id, new Cell(3, 1)));

            Assert.AreEqual(1, refus.Count);
            Assert.AreEqual(CommandError.NotYourTurn, refus[0]);
        }

        [Test]
        public void UnRefusNEstEnvoyeQuAuJoueurConcerne()
        {
            var refusAlice = new List<CommandError>();
            _clientAlice.CommandRejected += erreur => refusAlice.Add(erreur);
            var refusBob = new List<CommandError>();
            _clientBob.CommandRejected += erreur => refusBob.Add(erreur);

            _clientAlice.Send(new MoveCommand(_alice.Id, new Cell(6, 4)));

            Assert.AreEqual(1, refusAlice.Count);
            Assert.AreEqual(CommandError.Unreachable, refusAlice[0]);
            Assert.AreEqual(0, refusBob.Count);
        }

        [Test]
        public void UnSort_MetAJourLesPointsDeVieChezLesDeuxClients()
        {
            _clientAlice.Send(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));

            Assert.AreEqual(25, _clientAlice.State.Fighters[_bob.Id].Health);
            Assert.AreEqual(25, _clientBob.State.Fighters[_bob.Id].Health);
            Assert.AreEqual(3, _clientBob.State.Fighters[_alice.Id].ActionPoints);
        }

        [Test]
        public void FinDeTour_LeClientVoitLeNouveauCombattantActif()
        {
            _clientAlice.Send(new EndTurnCommand(_alice.Id));

            Assert.AreEqual(_bob.Id, _clientAlice.State.ActiveFighterId);
            Assert.AreEqual(_bob.Id, _clientBob.State.ActiveFighterId);
            Assert.AreEqual(3, _clientBob.State.Fighters[_bob.Id].MovementPoints);
        }

        [Test]
        public void UnePartieJoueeEnEntier_LaisseLesClientsDAccordAvecLeServeur()
        {
            // Alice tape jusqu'à ce que Bob tombe : 30 PV, 5 dégâts, 2 sorts par tour.
            for (int tour = 0; tour < 4 && !_battle.IsOver; tour++)
            {
                _clientAlice.Send(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));
                _clientAlice.Send(new CastSpellCommand(_alice.Id, DagueId, _bob.Position));
                if (_battle.IsOver) break;

                _clientAlice.Send(new EndTurnCommand(_alice.Id));
                _clientBob.Send(new EndTurnCommand(_bob.Id));
            }

            Assert.IsTrue(_battle.IsOver);
            Assert.AreEqual(0, _battle.WinningTeamId);

            foreach (BattleClient client in new[] { _clientAlice, _clientBob })
            {
                Assert.IsTrue(client.State.IsOver);
                Assert.AreEqual(0, client.State.WinningTeamId);
                Assert.AreEqual(0, client.State.Fighters[_bob.Id].Health);
                Assert.IsFalse(client.State.Fighters[_bob.Id].IsAlive);
                Assert.AreEqual(_alice.Health, client.State.Fighters[_alice.Id].Health);
                Assert.AreEqual(_alice.Position, client.State.Fighters[_alice.Id].Position);
            }
        }

        [Test]
        public void LaCarteDuClient_SeRejoueEnCarteDeCombat()
        {
            BattleMap carte = _clientAlice.State.BuildMap();

            Assert.AreEqual(7, carte.Width);
            Assert.IsFalse(carte.IsWalkable(new Cell(2, 2)));
            Assert.IsTrue(carte.IsWalkable(new Cell(0, 0)));
        }
    }

    [TestFixture]
    public class BattleCodecTests
    {
        [Test]
        public void Deplacement_SurviteALAllerRetour()
        {
            var commande = new MoveCommand(7, new Cell(3, 4));

            var decodee = (MoveCommand)BattleCodec.DecodeCommand(BattleCodec.EncodeCommand(commande));

            Assert.AreEqual(7, decodee.FighterId);
            Assert.AreEqual(new Cell(3, 4), decodee.Destination);
        }

        [Test]
        public void LancerDeSort_SurviteALAllerRetour()
        {
            var commande = new CastSpellCommand(7, 42, new Cell(1, 2));

            var decodee = (CastSpellCommand)BattleCodec.DecodeCommand(BattleCodec.EncodeCommand(commande));

            Assert.AreEqual(7, decodee.FighterId);
            Assert.AreEqual(42, decodee.SpellId);
            Assert.AreEqual(new Cell(1, 2), decodee.Target);
        }

        [Test]
        public void FinDeTour_SurviteALAllerRetour()
        {
            var decodee = BattleCodec.DecodeCommand(BattleCodec.EncodeCommand(new EndTurnCommand(9)));

            Assert.IsTrue(decodee is EndTurnCommand);
            Assert.AreEqual(9, decodee.FighterId);
        }

        [Test]
        public void TousLesEvenements_SurviventALAllerRetour()
        {
            var origine = new List<BattleEvent>
            {
                new TurnStarted(1, 3),
                new TurnEnded(2),
                new FighterMoved(1, new List<Cell> { new Cell(1, 0), new Cell(2, 0) }, 4),
                new SpellCast(1, 42, new Cell(2, 2), 3),
                new DamageTaken(2, 17, 13),
                new FighterDied(2),
                new BattleEnded(0),
            };

            List<BattleEvent> relus = BattleCodec.DecodeEvents(BattleCodec.EncodeEvents(origine));

            Assert.AreEqual(7, relus.Count);
            Assert.AreEqual(3, ((TurnStarted)relus[0]).Round);
            Assert.AreEqual(2, ((TurnEnded)relus[1]).FighterId);

            var deplacement = (FighterMoved)relus[2];
            Assert.AreEqual(2, deplacement.Path.Count);
            Assert.AreEqual(new Cell(2, 0), deplacement.Path[1]);
            Assert.AreEqual(4, deplacement.MovementPointsLeft);

            Assert.AreEqual(42, ((SpellCast)relus[3]).SpellId);
            Assert.AreEqual(17, ((DamageTaken)relus[4]).Amount);
            Assert.AreEqual(2, ((FighterDied)relus[5]).FighterId);
            Assert.AreEqual(0, ((BattleEnded)relus[6]).WinningTeamId);
        }

        [Test]
        public void UnMatchNul_GardeSonAbsenceDeVainqueur()
        {
            var relus = BattleCodec.DecodeEvents(
                BattleCodec.EncodeEvents(new List<BattleEvent> { new BattleEnded(null) }));

            Assert.IsNull(((BattleEnded)relus[0]).WinningTeamId);
        }

        [Test]
        public void UnRefus_SurviteALAllerRetour()
        {
            byte[] message = BattleCodec.EncodeRejection(CommandError.NoLineOfSight);

            Assert.AreEqual(MessageType.CommandRejected, BattleCodec.PeekType(message));
            Assert.AreEqual(CommandError.NoLineOfSight, BattleCodec.DecodeRejection(message));
        }

        [Test]
        public void UnMessageTronque_EstRejeteProprement()
        {
            byte[] complet = BattleCodec.EncodeCommand(new MoveCommand(7, new Cell(3, 4)));
            var tronque = new byte[complet.Length - 3];
            System.Array.Copy(complet, tronque, tronque.Length);

            Assert.Throws<ProtocolException>(() => BattleCodec.DecodeCommand(tronque));
        }

        [Test]
        public void UnMessageVide_EstRejeteProprement()
        {
            Assert.Throws<ProtocolException>(() => BattleCodec.PeekType(new byte[0]));
        }

        [Test]
        public void UnTypeDeMessageInconnu_EstRejeteProprement()
        {
            Assert.Throws<ProtocolException>(() => BattleCodec.PeekType(new byte[] { 99, 0, 0 }));
        }

        [Test]
        public void UnMessageDuMauvaisType_EstRejeteProprement()
        {
            byte[] evenements = BattleCodec.EncodeEvents(new List<BattleEvent> { new TurnEnded(1) });

            Assert.Throws<ProtocolException>(() => BattleCodec.DecodeCommand(evenements));
        }
    }
}
