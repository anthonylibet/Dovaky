using Dovaky.Combat;
using NUnit.Framework;

namespace Dovaky.Combat.Tests
{
    [TestFixture]
    public class LineOfSightTests
    {
        [Test]
        public void TerrainDegage_LaVuePasse()
        {
            BattleMap map = BattleMap.FromRows(".....", ".....", ".....");

            Assert.IsTrue(LineOfSight.HasLineOfSight(map, new Cell(0, 1), new Cell(4, 1)));
        }

        [Test]
        public void UnMurCoupeLaVue()
        {
            BattleMap map = BattleMap.FromRows(".....", "..#..", ".....");

            Assert.IsFalse(LineOfSight.HasLineOfSight(map, new Cell(0, 1), new Cell(4, 1)));
        }

        [Test]
        public void UnObstacleBasNeCoupePasLaVue()
        {
            BattleMap map = BattleMap.FromRows(".....", "..o..", ".....");

            Assert.IsTrue(LineOfSight.HasLineOfSight(map, new Cell(0, 1), new Cell(4, 1)));
        }

        [Test]
        public void LaCaseVisee_NeSeBloquePasElleMeme()
        {
            BattleMap map = BattleMap.FromRows(".....", "..#..", ".....");

            Assert.IsTrue(LineOfSight.HasLineOfSight(map, new Cell(0, 1), new Cell(2, 1)));
        }

        [Test]
        public void UnCombattantSurLeTrajetCoupeLaVue()
        {
            BattleMap map = BattleMap.FromRows(".....", ".....", ".....");
            var gene = new[] { new Cell(2, 1) };

            Assert.IsFalse(LineOfSight.HasLineOfSight(map, new Cell(0, 1), new Cell(4, 1), gene));
        }

        [Test]
        public void MemeCase_LaVuePasseToujours()
        {
            BattleMap map = BattleMap.FromRows(".....");

            Assert.IsTrue(LineOfSight.HasLineOfSight(map, new Cell(2, 0), new Cell(2, 0)));
        }

        [Test]
        public void ParUnCoin_LaVuePasseSiUnContournementEstDegage()
        {
            BattleMap map = BattleMap.FromRows(".#.", "...", "...");

            Assert.IsTrue(LineOfSight.HasLineOfSight(map, new Cell(0, 0), new Cell(1, 1)));
        }

        [Test]
        public void ParUnCoin_LaVueEstCoupeeSiLesDeuxContournementsSontFermes()
        {
            BattleMap map = BattleMap.FromRows(".#.", "#..", "...");

            Assert.IsFalse(LineOfSight.HasLineOfSight(map, new Cell(0, 0), new Cell(1, 1)));
        }
    }
}
