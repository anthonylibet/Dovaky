using System;
using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Coordonnée logique sur la grille de combat.
    /// La grille est un quadrillage carré : c'est l'affichage qui la rend
    /// isométrique (losanges). Les déplacements suivent les quatre voisins
    /// orthogonaux, ce qui correspond aux quatre directions d'un Dofus-like.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        private static readonly Cell[] DirectionsArray =
        {
            new Cell(1, 0),
            new Cell(-1, 0),
            new Cell(0, 1),
            new Cell(0, -1),
        };

        /// <summary>
        /// Les quatre directions de déplacement, dans un ordre fixe : le
        /// parcours des voisins est donc déterministe, condition nécessaire
        /// pour que serveur et client calculent exactement le même chemin.
        /// </summary>
        public static IReadOnlyList<Cell> Directions => DirectionsArray;

        /// <summary>Distance de Manhattan, qui est la distance de jeu sur cette grille.</summary>
        public static int Distance(Cell a, Cell b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        public Cell Translate(int dx, int dy) => new Cell(X + dx, Y + dy);

        public Cell Translate(Cell direction) => new Cell(X + direction.X, Y + direction.Y);

        public bool Equals(Cell other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
