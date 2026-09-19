using System;
using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Ligne de vue entre deux cases. On trace le segment reliant les centres
    /// des deux cases : une case intermédiaire qui bloque la vue coupe la ligne.
    /// Les cases de départ et d'arrivée ne bloquent jamais — on voit depuis
    /// sa propre case, et on peut viser un ennemi même s'il est lui-même un
    /// obstacle pour les autres.
    /// </summary>
    public static class LineOfSight
    {
        public static bool HasLineOfSight(
            BattleMap map,
            Cell from,
            Cell to,
            IReadOnlyCollection<Cell> sightBlockingCells = null)
        {
            if (from == to) return true;

            var blockers = new HashSet<Cell>();
            if (sightBlockingCells != null)
            {
                foreach (Cell cell in sightBlockingCells) blockers.Add(cell);
            }

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            int nx = Math.Abs(dx);
            int ny = Math.Abs(dy);
            int signX = Math.Sign(dx);
            int signY = Math.Sign(dy);

            Cell current = from;
            int ix = 0;
            int iy = 0;

            while (ix < nx || iy < ny)
            {
                // Comparaison en entiers de (ix + 0.5) / nx et (iy + 0.5) / ny :
                // on avance sur l'axe en retard, sans arithmétique flottante,
                // pour que le résultat soit strictement identique partout.
                long decision = (long)(1 + 2 * ix) * ny - (long)(1 + 2 * iy) * nx;

                if (decision == 0)
                {
                    // Le segment passe exactement par un coin. Règle permissive :
                    // la vue passe si l'un des deux contournements est dégagé.
                    Cell sideA = new Cell(current.X + signX, current.Y);
                    Cell sideB = new Cell(current.X, current.Y + signY);
                    if (Blocks(map, blockers, sideA, from, to) && Blocks(map, blockers, sideB, from, to))
                    {
                        return false;
                    }

                    current = new Cell(current.X + signX, current.Y + signY);
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    current = new Cell(current.X + signX, current.Y);
                    ix++;
                }
                else
                {
                    current = new Cell(current.X, current.Y + signY);
                    iy++;
                }

                if (current == to) return true;
                if (Blocks(map, blockers, current, from, to)) return false;
            }

            return true;
        }

        private static bool Blocks(BattleMap map, HashSet<Cell> blockers, Cell cell, Cell from, Cell to)
        {
            if (cell == from || cell == to) return false;
            return map.BlocksSight(cell) || blockers.Contains(cell);
        }
    }
}
