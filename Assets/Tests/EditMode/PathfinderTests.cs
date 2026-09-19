using Dovaky.Combat;
using NUnit.Framework;

namespace Dovaky.Combat.Tests
{
    [TestFixture]
    public class PathfinderTests
    {
        [Test]
        public void FindPath_SurTerrainDegage_SuitLaLigneDroite()
        {
            BattleMap map = BattleMap.FromRows(".....", ".....", ".....");

            var path = Pathfinder.FindPath(map, new Cell(0, 0), new Cell(3, 0), 5);

            Assert.IsNotNull(path);
            Assert.AreEqual(3, path.Count);
            Assert.AreEqual(new Cell(3, 0), path[path.Count - 1]);
        }

        [Test]
        public void FindPath_LaCaseDeDepartEstExclueDuChemin()
        {
            BattleMap map = BattleMap.FromRows(".....");

            var path = Pathfinder.FindPath(map, new Cell(0, 0), new Cell(1, 0), 5);

            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(new Cell(1, 0), path[0]);
        }

        [Test]
        public void FindPath_ContourneUnMur()
        {
            // Le mur coupe la ligne directe : il faut passer par un bord.
            BattleMap map = BattleMap.FromRows(
                ".....",
                ".###.",
                ".....");

            var path = Pathfinder.FindPath(map, new Cell(2, 0), new Cell(2, 2), 10);

            Assert.IsNotNull(path);
            Assert.AreEqual(6, path.Count);
        }

        [Test]
        public void FindPath_SansAssezDePm_EchoueMemeSiLeCheminExiste()
        {
            BattleMap map = BattleMap.FromRows(
                ".....",
                ".###.",
                ".....");

            Assert.IsNull(Pathfinder.FindPath(map, new Cell(2, 0), new Cell(2, 2), 5));
            Assert.IsNotNull(Pathfinder.FindPath(map, new Cell(2, 0), new Cell(2, 2), 6));
        }

        [Test]
        public void FindPath_UneCaseOccupeeBloqueLePassage()
        {
            BattleMap map = BattleMap.FromRows("...");
            var occupees = new[] { new Cell(1, 0) };

            Assert.IsNull(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(2, 0), 5, occupees));
        }

        [Test]
        public void FindPath_VersUnObstacle_EstRefuse()
        {
            BattleMap map = BattleMap.FromRows(".o.");

            Assert.IsNull(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(1, 0), 5));
        }

        [Test]
        public void ReachableCells_FormeUnLosangeDeRayonEgalAuxPm()
        {
            BattleMap map = BattleMap.FromRows(
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......",
                ".......");

            var atteignables = Pathfinder.ReachableCells(map, new Cell(3, 3), 2);

            // 1 case d'origine + 4 à distance 1 + 8 à distance 2.
            Assert.AreEqual(13, atteignables.Count);
            Assert.AreEqual(0, atteignables[new Cell(3, 3)]);
            Assert.AreEqual(2, atteignables[new Cell(5, 3)]);
        }

        [Test]
        public void ReachableCells_SansPm_NeRendQueLOrigine()
        {
            BattleMap map = BattleMap.FromRows("...", "...", "...");

            var atteignables = Pathfinder.ReachableCells(map, new Cell(1, 1), 0);

            Assert.AreEqual(1, atteignables.Count);
        }
    }
}
