using Dovaky.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dovaky.Game
{
    /// <summary>
    /// Pont entre la grille logique et la scène Unity. Toute la trigonométrie
    /// vit dans <see cref="IsoProjection"/>, côté C# pur et testé : ici on ne
    /// fait que l'habiller en <see cref="Vector3"/>, pour que l'affichage et
    /// la visée à la souris ne puissent pas diverger.
    ///
    /// Les cases sont dessinées avec des <see cref="SpriteRenderer"/> et non
    /// des meshes : Unity leur donne automatiquement le matériau sprite du
    /// pipeline actif. Chercher un shader par son nom, à l'inverse, est fragile
    /// — « Sprites/Default » existe en URP mais n'a pas de passe Universal2D,
    /// donc le 2D Renderer ne le dessine pas du tout.
    /// </summary>
    public static class IsoGrid
    {
        private static Sprite _whiteSprite;

        public static Vector3 CellToWorld(Cell cell, float tileWidth, float tileHeight, float z = 0f)
        {
            PlanePoint point = IsoProjection.CellToPlane(cell, tileWidth, tileHeight);
            return new Vector3(point.X, point.Y, z);
        }

        public static Cell WorldToCell(Vector3 world, float tileWidth, float tileHeight)
        {
            return IsoProjection.PlaneToCell(world.x, world.y, tileWidth, tileHeight);
        }

        /// <summary>Case sous le curseur. La caméra doit être orthographique et de face.</summary>
        public static Cell ScreenToCell(Camera camera, Vector3 screenPosition, float tileWidth, float tileHeight)
        {
            Vector3 world = camera.ScreenToWorldPoint(screenPosition);
            return WorldToCell(world, tileWidth, tileHeight);
        }

        /// <summary>
        /// Sprite blanc d'une unité, teinté ensuite par chaque renderer. Généré
        /// à l'exécution : le projet s'affiche sans le moindre asset d'art.
        /// </summary>
        public static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;

            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _whiteSprite.hideFlags = HideFlags.DontSave;
            return _whiteSprite;
        }

        /// <summary>
        /// Crée un losange isométrique et renvoie sa racine.
        ///
        /// Le losange est un carré tourné à 45°, puis écrasé verticalement.
        /// L'écrasement est porté par un objet parent parce qu'Unity applique
        /// l'échelle AVANT la rotation : une échelle non uniforme sur l'objet
        /// tourné donnerait un parallélogramme de travers, pas un losange.
        /// </summary>
        public static GameObject CreateDiamond(
            string name,
            Transform parent,
            Vector3 localPosition,
            float tileWidth,
            float tileHeight,
            Color color,
            int sortingOrder,
            float fill = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.localPosition = localPosition;
            root.transform.localScale = new Vector3(1f, tileHeight / tileWidth, 1f);

            var shape = new GameObject("Shape");
            shape.transform.SetParent(root.transform, worldPositionStays: false);
            shape.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Un carré de côté c tourné à 45° occupe une largeur c * racine(2).
            float side = tileWidth * fill / Mathf.Sqrt(2f);
            shape.transform.localScale = new Vector3(side, side, 1f);

            SpriteRenderer renderer = shape.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return root;
        }

        public static void SetColor(GameObject diamond, Color color)
        {
            if (diamond == null) return;

            SpriteRenderer renderer = diamond.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null) renderer.color = color;
        }

        /// <summary>Nom du pipeline de rendu actif, pour les diagnostics.</summary>
        public static string ActivePipelineName()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline == null ? "Built-in (aucun URP Asset assigné)" : pipeline.GetType().Name;
        }
    }
}
