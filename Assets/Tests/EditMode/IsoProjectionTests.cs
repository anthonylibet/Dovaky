using Dovaky.Combat;
using NUnit.Framework;

namespace Dovaky.Combat.Tests
{
    [TestFixture]
    public class IsoProjectionTests
    {
        [Test]
        public void LOrigineDeLaGrilleEstALOrigineDuMonde()
        {
            PlanePoint point = IsoProjection.CellToPlane(new Cell(0, 0));

            Assert.AreEqual(0f, point.X);
            Assert.AreEqual(0f, point.Y);
        }

        [Test]
        public void AvancerEnXVaVersLaDroiteEtVersLeBas()
        {
            PlanePoint point = IsoProjection.CellToPlane(new Cell(1, 0));

            Assert.IsTrue(point.X > 0f);
            Assert.IsTrue(point.Y < 0f);
        }

        [Test]
        public void AvancerEnYVaVersLaGaucheEtVersLeBas()
        {
            PlanePoint point = IsoProjection.CellToPlane(new Cell(0, 1));

            Assert.IsTrue(point.X < 0f);
            Assert.IsTrue(point.Y < 0f);
        }

        [Test]
        public void AllerRetour_RetombeSurLaMemeCase()
        {
            for (int x = 0; x < 12; x++)
            {
                for (int y = 0; y < 12; y++)
                {
                    var origine = new Cell(x, y);
                    Cell retour = IsoProjection.PlaneToCell(IsoProjection.CellToPlane(origine));
                    Assert.AreEqual(origine, retour);
                }
            }
        }

        [Test]
        public void UnPointProcheDuCentre_DesigneLaMemeCase()
        {
            PlanePoint centre = IsoProjection.CellToPlane(new Cell(3, 2));

            Cell touchee = IsoProjection.PlaneToCell(centre.X + 0.1f, centre.Y + 0.05f);

            Assert.AreEqual(new Cell(3, 2), touchee);
        }

        [Test]
        public void DesCasesVoisinesOccupentDesPositionsDistinctes()
        {
            PlanePoint a = IsoProjection.CellToPlane(new Cell(2, 2));
            PlanePoint b = IsoProjection.CellToPlane(new Cell(3, 2));
            PlanePoint c = IsoProjection.CellToPlane(new Cell(2, 3));

            Assert.AreNotEqual(a.X, b.X);
            Assert.AreNotEqual(a.X, c.X);
            Assert.AreNotEqual(b.X, c.X);
        }

        [Test]
        public void UneTailleDeTuilePersonnaliseeEstRespectee()
        {
            PlanePoint point = IsoProjection.CellToPlane(new Cell(1, 0), tileWidth: 2f, tileHeight: 1f);

            Assert.AreEqual(1f, point.X);
            Assert.AreEqual(-0.5f, point.Y);
            Assert.AreEqual(new Cell(1, 0), IsoProjection.PlaneToCell(point, tileWidth: 2f, tileHeight: 1f));
        }
    }
}
