using System.Collections.Generic;
using Dovaky.Combat;
using Dovaky.Combat.Net;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Le décor : une tuile par case, plus une couche de surbrillance pour la
    /// zone de déplacement ou la portée d'un sort. Ne décide de rien, se
    /// contente de rendre l'état que le client a reçu.
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private readonly Dictionary<Cell, MeshRenderer> _tiles = new Dictionary<Cell, MeshRenderer>();
        private readonly List<GameObject> _highlights = new List<GameObject>();

        private Mesh _cellMesh;
        private Material _floorMaterial;
        private Material _obstacleMaterial;
        private Material _wallMaterial;
        private Material _highlightMaterial;

        private float _tileWidth = IsoProjection.DefaultTileWidth;
        private float _tileHeight = IsoProjection.DefaultTileHeight;

        public float TileWidth => _tileWidth;

        public float TileHeight => _tileHeight;

        public void Build(ClientBattleState state, float tileWidth, float tileHeight)
        {
            Clear();

            _tileWidth = tileWidth;
            _tileHeight = tileHeight;
            _cellMesh = IsoGrid.CreateCellMesh(tileWidth, tileHeight);
            _floorMaterial = IsoGrid.CreateUnlitMaterial(new Color(0.22f, 0.25f, 0.3f));
            _obstacleMaterial = IsoGrid.CreateUnlitMaterial(new Color(0.45f, 0.38f, 0.25f));
            _wallMaterial = IsoGrid.CreateUnlitMaterial(new Color(0.12f, 0.12f, 0.15f));
            _highlightMaterial = IsoGrid.CreateUnlitMaterial(new Color(0.3f, 0.7f, 1f, 0.45f));

            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var cell = new Cell(x, y);
                    Material material;
                    switch (state.GetCell(cell))
                    {
                        case CellKind.Wall: material = _wallMaterial; break;
                        case CellKind.Obstacle: material = _obstacleMaterial; break;
                        default: material = _floorMaterial; break;
                    }

                    _tiles[cell] = CreateQuad("Cell " + cell, cell, material, z: 0f);
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
                MeshRenderer highlight = CreateQuad("Highlight " + cell, cell, _highlightMaterial, z: -0.01f);
                _highlights.Add(highlight.gameObject);
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

        public Vector3 WorldPositionOf(Cell cell, float z = -0.1f)
        {
            return IsoGrid.CellToWorld(cell, _tileWidth, _tileHeight, z);
        }

        public void Clear()
        {
            ClearHighlights();
            foreach (MeshRenderer tile in _tiles.Values)
            {
                if (tile != null) Destroy(tile.gameObject);
            }

            _tiles.Clear();
        }

        private MeshRenderer CreateQuad(string name, Cell cell, Material material, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = IsoGrid.CellToWorld(cell, _tileWidth, _tileHeight, z);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _cellMesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void OnDestroy() => Clear();
    }
}
