using System;

namespace Dovaky.Combat
{
    /// <summary>Nature d'une case de la grille.</summary>
    public enum CellKind
    {
        /// <summary>Case libre : on la traverse et on voit au travers.</summary>
        Floor = 0,

        /// <summary>Obstacle bas (caisse, rocher) : bloque le déplacement, pas la ligne de vue.</summary>
        Obstacle = 1,

        /// <summary>Mur : bloque le déplacement et la ligne de vue.</summary>
        Wall = 2,
    }

    /// <summary>
    /// Terrain d'un combat. Ne contient que le décor : les combattants vivent
    /// dans <see cref="Battle"/>, pour que la même carte serve à plusieurs combats.
    /// </summary>
    public sealed class BattleMap
    {
        private readonly CellKind[] _cells;

        public int Width { get; }

        public int Height { get; }

        public BattleMap(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _cells = new CellKind[width * height];
        }

        /// <summary>
        /// Construit une carte depuis une représentation texte, une chaîne par
        /// ligne : '.' case libre, 'o' obstacle bas, '#' mur.
        /// La première chaîne est la ligne y = 0.
        /// </summary>
        public static BattleMap FromRows(params string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("Carte vide.", nameof(rows));

            int width = rows[0].Length;
            var map = new BattleMap(width, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != width)
                    throw new ArgumentException("Toutes les lignes doivent avoir la même longueur.", nameof(rows));

                for (int x = 0; x < width; x++)
                {
                    CellKind kind;
                    switch (rows[y][x])
                    {
                        case '.': kind = CellKind.Floor; break;
                        case 'o': kind = CellKind.Obstacle; break;
                        case '#': kind = CellKind.Wall; break;
                        default: throw new ArgumentException("Caractère de carte inconnu : " + rows[y][x], nameof(rows));
                    }

                    map.SetCell(new Cell(x, y), kind);
                }
            }

            return map;
        }

        public bool Contains(Cell cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        public CellKind GetCell(Cell cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return _cells[cell.Y * Width + cell.X];
        }

        public void SetCell(Cell cell, CellKind kind)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            _cells[cell.Y * Width + cell.X] = kind;
        }

        /// <summary>Une case hors carte n'est ni traversable ni ciblable.</summary>
        public bool IsWalkable(Cell cell) => Contains(cell) && GetCell(cell) == CellKind.Floor;

        public bool BlocksSight(Cell cell) => !Contains(cell) || GetCell(cell) == CellKind.Wall;
    }
}
