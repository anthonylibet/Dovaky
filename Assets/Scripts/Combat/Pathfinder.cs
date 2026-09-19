using System.Collections.Generic;

namespace Dovaky.Combat
{
    /// <summary>
    /// Déplacement sur la grille : un pas orthogonal coûte 1 PM.
    /// Le parcours est un BFS dont l'ordre des voisins est fixe
    /// (<see cref="Cell.Directions"/>), donc deux exécutions renvoient toujours
    /// le même chemin — indispensable pour que le client affiche exactement le
    /// trajet que le serveur a validé.
    /// </summary>
    public static class Pathfinder
    {
        /// <summary>
        /// Cases atteignables depuis <paramref name="origin"/> avec au plus
        /// <paramref name="maxCost"/> PM, associées à leur coût. L'origine est
        /// incluse avec un coût de 0. Sert aussi à l'affichage de la zone de
        /// déplacement.
        /// </summary>
        public static Dictionary<Cell, int> ReachableCells(
            BattleMap map,
            Cell origin,
            int maxCost,
            IReadOnlyCollection<Cell> blockedCells = null)
        {
            var costs = new Dictionary<Cell, int> { [origin] = 0 };
            if (maxCost <= 0) return costs;

            var blocked = AsSet(blockedCells);
            var queue = new Queue<Cell>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();
                int nextCost = costs[current] + 1;
                if (nextCost > maxCost) continue;

                var directions = Cell.Directions;
                for (int i = 0; i < directions.Count; i++)
                {
                    Cell neighbour = current.Translate(directions[i]);
                    if (costs.ContainsKey(neighbour)) continue;
                    if (!map.IsWalkable(neighbour)) continue;
                    if (blocked.Contains(neighbour)) continue;

                    costs[neighbour] = nextCost;
                    queue.Enqueue(neighbour);
                }
            }

            return costs;
        }

        /// <summary>
        /// Chemin le plus court de <paramref name="origin"/> à
        /// <paramref name="destination"/>, hors case de départ, ou null si la
        /// destination est inatteignable dans la limite de
        /// <paramref name="maxCost"/> PM. La longueur de la liste est le coût
        /// en PM du déplacement.
        /// </summary>
        public static List<Cell> FindPath(
            BattleMap map,
            Cell origin,
            Cell destination,
            int maxCost,
            IReadOnlyCollection<Cell> blockedCells = null)
        {
            if (origin == destination) return new List<Cell>();
            if (!map.IsWalkable(destination)) return null;

            var blocked = AsSet(blockedCells);
            if (blocked.Contains(destination)) return null;

            var cameFrom = new Dictionary<Cell, Cell>();
            var costs = new Dictionary<Cell, int> { [origin] = 0 };
            var queue = new Queue<Cell>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();
                int nextCost = costs[current] + 1;
                if (nextCost > maxCost) continue;

                var directions = Cell.Directions;
                for (int i = 0; i < directions.Count; i++)
                {
                    Cell neighbour = current.Translate(directions[i]);
                    if (costs.ContainsKey(neighbour)) continue;
                    if (!map.IsWalkable(neighbour)) continue;
                    if (blocked.Contains(neighbour)) continue;

                    costs[neighbour] = nextCost;
                    cameFrom[neighbour] = current;

                    if (neighbour == destination) return BuildPath(cameFrom, origin, destination);

                    queue.Enqueue(neighbour);
                }
            }

            return null;
        }

        private static List<Cell> BuildPath(Dictionary<Cell, Cell> cameFrom, Cell origin, Cell destination)
        {
            var path = new List<Cell>();
            Cell current = destination;
            while (current != origin)
            {
                path.Add(current);
                current = cameFrom[current];
            }

            path.Reverse();
            return path;
        }

        private static HashSet<Cell> AsSet(IReadOnlyCollection<Cell> cells)
        {
            var set = new HashSet<Cell>();
            if (cells == null) return set;

            foreach (Cell cell in cells) set.Add(cell);
            return set;
        }
    }
}
