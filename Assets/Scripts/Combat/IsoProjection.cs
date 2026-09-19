using System;

namespace Dovaky.Combat
{
    /// <summary>Point sur le plan de jeu, en unités monde, sans dépendance Unity.</summary>
    public readonly struct PlanePoint
    {
        public readonly float X;
        public readonly float Y;

        public PlanePoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>
    /// Passage entre la grille logique et l'affichage isométrique. La grille
    /// reste un quadrillage carré : seule la projection lui donne sa forme de
    /// losange à l'écran. Les deux sens sont ici pour que le rendu et la visée
    /// à la souris partagent exactement la même convention.
    /// </summary>
    public static class IsoProjection
    {
        public const float DefaultTileWidth = 1f;
        public const float DefaultTileHeight = 0.5f;

        /// <summary>Centre de la case, en unités monde.</summary>
        public static PlanePoint CellToPlane(Cell cell, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight)
        {
            float x = (cell.X - cell.Y) * tileWidth * 0.5f;
            float y = -(cell.X + cell.Y) * tileHeight * 0.5f;
            return new PlanePoint(x, y);
        }

        /// <summary>
        /// Case contenant ce point. L'axe Y descend quand on s'éloigne dans la
        /// grille, ce qui place les cases du fond plus haut à l'écran.
        /// </summary>
        public static Cell PlaneToCell(float x, float y, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight)
        {
            if (tileWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(tileWidth));
            if (tileHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(tileHeight));

            // On inverse le système : u = X - Y, v = X + Y.
            double u = 2.0 * x / tileWidth;
            double v = -2.0 * y / tileHeight;
            int cellX = (int)Math.Round((u + v) * 0.5, MidpointRounding.AwayFromZero);
            int cellY = (int)Math.Round((v - u) * 0.5, MidpointRounding.AwayFromZero);
            return new Cell(cellX, cellY);
        }

        public static Cell PlaneToCell(PlanePoint point, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight)
        {
            return PlaneToCell(point.X, point.Y, tileWidth, tileHeight);
        }
    }
}
