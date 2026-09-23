using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Le décor : une tuile par case, plus une couche de surbrillance pour la
    /// zone de déplacement. Ne décide de rien, se contente de rendre l'état que
    /// le client a reçu.
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private const int TileSortingOrder = 0;
        private const int HighlightSortingOrder = 10;

        private static readonly Color FloorColor = new Color(0.42f, 0.47f, 0.56f);
        private static readonly Color ObstacleColor = new Color(0.62f, 0.5f, 0.32f);
        private static readonly Color WallColor = new Color(0.16f, 0.16f, 0.2f);
        private static readonly Color HighlightColor = new Color(0.35f, 0.8f, 1f, 0.55f);

        private readonly Dictionary<Cell, GameObject> _tiles = new Dictionary<Cell, GameObject>();
        private readonly List<GameObject> _highlights = new List<GameObject>();

        private float _tileWidth = IsoProjection.DefaultTileWidth;
        private float _tileHeight = IsoProjection.DefaultTileHeight;

        public float TileWidth => _tileWidth;

        public float TileHeight => _tileHeight;

        /// <summary>Nombre de tuiles construites, utile pour diagnostiquer un écran vide.</summary>
        public int TileCount => _tiles.Count;

        public void Build(ClientBattleState state, float tileWidth, float tileHeight)
        {
            Clear();

            _tileWidth = tileWidth;
            _tileHeight = tileHeight;

            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var cell = new Cell(x, y);
                    Color color;
                    switch (state.GetCell(cell))
                    {
                        case CellKind.Wall: color = WallColor; break;
                        case CellKind.Obstacle: color = ObstacleColor; break;
                        default: color = FloorColor; break;
                    }

                    _tiles[cell] = IsoGrid.CreateDiamond(
                        "Cell " + cell,
                        transform,
                        IsoGrid.CellToWorld(cell, _tileWidth, _tileHeight),
                        _tileWidth,
                        _tileHeight,
                        color,
                        TileSortingOrder,
                        fill: 0.94f);
                }
            }
        }

        /// <summary>Éclaire un ensemble de cases ; l'appel suivant remplace le précédent.</summary>
        public void Highlight(IEnumerable<Cell> cells)
        {
            ClearHighlights();
            if (cells == null) return;

            foreach (Cell cell in cells)
            {
                _highlights.Add(IsoGrid.CreateDiamond(
                    "Highlight " + cell,
                    transform,
                    IsoGrid.CellToWorld(cell, _tileWidth, _tileHeight),
                    _tileWidth,
                    _tileHeight,
                    HighlightColor,
                    HighlightSortingOrder,
                    fill: 0.8f));
            }
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _highlights.Count; i++)
            {
                if (_highlights[i] != null) Destroy(_highlights[i]);
            }

            _highlights.Clear();
        }

        public Vector3 WorldPositionOf(Cell cell, float z = 0f)
        {
            return IsoGrid.CellToWorld(cell, _tileWidth, _tileHeight, z);
        }

        public void Clear()
        {
            ClearHighlights();
            foreach (GameObject tile in _tiles.Values)
            {
                if (tile != null) Destroy(tile);
            }

            _tiles.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
