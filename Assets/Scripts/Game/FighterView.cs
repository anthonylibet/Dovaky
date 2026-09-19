using System.Collections;
using System.Collections.Generic;
using Dovaky.Combat;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>Représentation d'un combattant : un losange coloré par équipe.</summary>
    public sealed class FighterView : MonoBehaviour
    {
        private BattleFieldView _field;

        public int FighterId { get; private set; }

        public Cell Cell { get; private set; }

        public void Initialise(int fighterId, Cell cell, Color color, BattleFieldView field)
        {
            FighterId = fighterId;
            _field = field;
            Cell = cell;

            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = IsoGrid.CreateCellMesh(field.TileWidth * 0.6f, field.TileHeight * 1.6f);

            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = IsoGrid.CreateUnlitMaterial(color);

            transform.position = _field.WorldPositionOf(cell);
        }

        public void Teleport(Cell cell)
        {
            Cell = cell;
            transform.position = _field.WorldPositionOf(cell);
        }

        /// <summary>Parcourt le chemin case par case, à vitesse constante.</summary>
        public IEnumerator MoveAlong(IReadOnlyList<Cell> path, float cellsPerSecond)
        {
            if (path == null || path.Count == 0) yield break;

            float secondsPerCell = cellsPerSecond <= 0f ? 0f : 1f / cellsPerSecond;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 from = transform.position;
                Vector3 to = _field.WorldPositionOf(path[i]);

                for (float elapsed = 0f; elapsed < secondsPerCell; elapsed += Time.deltaTime)
                {
                    transform.position = Vector3.Lerp(from, to, elapsed / secondsPerCell);
                    yield return null;
                }

                transform.position = to;
                Cell = path[i];
            }
        }

        public void SetDead()
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Color faded = renderer.sharedMaterial.color;
                faded.a = 0.25f;
                renderer.sharedMaterial.color = faded;
            }
        }
    }
}
