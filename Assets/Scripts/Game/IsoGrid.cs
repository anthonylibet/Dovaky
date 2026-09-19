using Dovaky.Combat;
using UnityEngine;

namespace Dovaky.Game
{
    /// <summary>
    /// Pont entre la grille logique et la scène Unity. Toute la trigonométrie
    /// vit dans <see cref="IsoProjection"/>, côté C# pur et testé : ici on ne
    /// fait que l'habiller en <see cref="Vector3"/>, pour que l'affichage et
    /// la visée à la souris ne puissent pas diverger.
    /// </summary>
    public static class IsoGrid
    {
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
        /// Losange d'une case, centré sur l'origine. Un mesh généré évite de
        /// dépendre du moindre asset : le projet tourne avant d'avoir des
        /// graphismes.
        /// </summary>
        public static Mesh CreateCellMesh(float tileWidth, float tileHeight)
        {
            float halfWidth = tileWidth * 0.5f;
            float halfHeight = tileHeight * 0.5f;

            var mesh = new Mesh { name = "IsoCell" };
            mesh.vertices = new[]
            {
                new Vector3(0f, halfHeight, 0f),
                new Vector3(halfWidth, 0f, 0f),
                new Vector3(0f, -halfHeight, 0f),
                new Vector3(-halfWidth, 0f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0.5f),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Matériau non éclairé pour ces losanges. On essaie plusieurs shaders :
        /// le projet doit s'afficher que le URP Asset 2D ait été créé ou non.
        /// </summary>
        public static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning("Aucun shader non éclairé trouvé : les cases resteront invisibles.");
                return null;
            }

            return new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
        }
    }
}
