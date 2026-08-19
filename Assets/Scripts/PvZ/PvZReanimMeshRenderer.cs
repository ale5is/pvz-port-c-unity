using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PvZReanimMeshRenderer : MonoBehaviour
    {
        // =========================================================
        // CONFIG
        // =========================================================

        /*
         * Reanim trabaja principalmente en píxeles.
         *
         * En Unity queremos:
         *
         * 100 píxeles = 1 unidad
         *
         * Por eso:
         *
         * 1 píxel = 0.01 unidades
         */
        private const float REANIM_PIXELS_PER_UNIT = 100f;

        private const float REANIM_PIXEL_TO_UNIT =
            1f / REANIM_PIXELS_PER_UNIT;

        // =========================================================
        // COMPONENTS
        // =========================================================

        private MeshFilter meshFilter;

        private MeshRenderer meshRenderer;

        private Mesh mesh;

        // =========================================================
        // MESH DATA
        // =========================================================

        private readonly Vector3[] vertices =
            new Vector3[4];

        private readonly Vector2[] uvs =
            new Vector2[4];

        private readonly int[] triangles =
        {
            0, 1, 2,
            2, 3, 0
        };

        // =========================================================
        // MATERIAL
        // =========================================================

        private Texture2D currentTexture;

        private Material material;

        private bool initialized;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            Initialize();
        }

        // =========================================================
        // INITIALIZATION
        // =========================================================

        private void Initialize()
        {
            if (initialized)
                return;

            meshFilter =
                GetComponent<MeshFilter>();

            meshRenderer =
                GetComponent<MeshRenderer>();

            mesh =
                new Mesh();

            mesh.name =
                "PvZ Reanim Track Mesh";

            mesh.MarkDynamic();

            meshFilter.sharedMesh =
                mesh;

            initialized =
                true;
        }

        // =========================================================
        // APPLY
        // =========================================================

        public void Apply(
            Sprite sprite,
            PvZReanimTransform transformData,
            PvZReanimTrackInstance instance)
        {
            if (sprite == null ||
                transformData == null ||
                instance == null)
            {
                Hide();
                return;
            }

            Initialize();

            // -----------------------------------------------------
            // CONSTRUIR MESH
            // -----------------------------------------------------

            BuildMesh(
                sprite,
                transformData
            );

            // -----------------------------------------------------
            // MATERIAL
            // -----------------------------------------------------

            UpdateMaterial(
                sprite.texture
            );

            // -----------------------------------------------------
            // COLOR
            // -----------------------------------------------------

            UpdateColor(
                transformData,
                instance
            );

            // -----------------------------------------------------
            // VISIBILIDAD
            // -----------------------------------------------------

            meshRenderer.enabled =
                instance.renderGroup !=
                PvZReanimRenderGroup.Hidden;
        }

        // =========================================================
        // BUILD MESH
        // =========================================================

        private void BuildMesh(
            Sprite sprite,
            PvZReanimTransform transformData)
        {
            if (sprite == null ||
                transformData == null)
            {
                return;
            }

            Rect rect =
                sprite.rect;

            /*
             * IMPORTANTE:
             *
             * NO usamos sprite.pixelsPerUnit.
             *
             * El Sprite puede venir del PAK con PPU = 1,
             * 100, etc.
             *
             * Reanim está trabajando en píxeles, así que
             * nosotros hacemos siempre:
             *
             * píxeles / 100 = unidades Unity.
             */

            float width =
                rect.width *
                REANIM_PIXEL_TO_UNIT;

            float height =
                rect.height *
                REANIM_PIXEL_TO_UNIT;

            // -----------------------------------------------------
            // PIVOT
            // -----------------------------------------------------

            Vector2 pivot =
                sprite.pivot;

            float left =
                -pivot.x *
                REANIM_PIXEL_TO_UNIT;

            float bottom =
                -pivot.y *
                REANIM_PIXEL_TO_UNIT;

            float right =
                left +
                width;

            float top =
                bottom +
                height;

            // -----------------------------------------------------
            // VERTICES
            // -----------------------------------------------------

            vertices[0] =
                new Vector3(
                    left,
                    bottom,
                    0f
                );

            vertices[1] =
                new Vector3(
                    right,
                    bottom,
                    0f
                );

            vertices[2] =
                new Vector3(
                    right,
                    top,
                    0f
                );

            vertices[3] =
                new Vector3(
                    left,
                    top,
                    0f
                );

            // -----------------------------------------------------
            // REANIM MATRIX
            // -----------------------------------------------------

            float x =
                transformData.GetX();

            float y =
                transformData.GetY();

            float skewX =
                transformData.GetSkewX();

            float skewY =
                transformData.GetSkewY();

            float scaleX =
                transformData.GetScaleX();

            float scaleY =
                transformData.GetScaleY();

            /*
             * Construimos la matriz aquí en lugar de usar
             * directamente PvZReanimMatrix.FromTransform().
             *
             * La diferencia importante es que x/y son píxeles.
             *
             * Ejemplo:
             *
             * Reanim:
             *     x = 50
             *
             * Unity:
             *     x = 0.50
             */

            float radiansX =
                -skewX *
                Mathf.Deg2Rad;

            float radiansY =
                -skewY *
                Mathf.Deg2Rad;

            float cosX =
                Mathf.Cos(radiansX);

            float sinX =
                Mathf.Sin(radiansX);

            float cosY =
                Mathf.Cos(radiansY);

            float sinY =
                Mathf.Sin(radiansY);

            float m00 =
                cosX *
                scaleX;

            float m01 =
                sinY *
                scaleY;

            float m10 =
                -sinX *
                scaleX;

            float m11 =
                cosY *
                scaleY;

            /*
             * x/y convertidos de píxeles a unidades Unity.
             */

            float translationX =
                x *
                REANIM_PIXEL_TO_UNIT;

            float translationY =
                y *
                REANIM_PIXEL_TO_UNIT;

            // -----------------------------------------------------
            // APLICAR MATRIZ A CADA VERTEX
            // -----------------------------------------------------

            for (int i = 0;
                 i < vertices.Length;
                 i++)
            {
                Vector3 vertex =
                    vertices[i];

                float transformedX =
                    m00 * vertex.x +
                    m01 * vertex.y +
                    translationX;

                float transformedY =
                    m10 * vertex.x +
                    m11 * vertex.y +
                    translationY;

                vertices[i] =
                    new Vector3(
                        transformedX,
                        transformedY,
                        0f
                    );
            }

            // -----------------------------------------------------
            // UV
            // -----------------------------------------------------

            BuildUVs(
                sprite
            );

            // -----------------------------------------------------
            // ACTUALIZAR MESH
            // -----------------------------------------------------

            mesh.Clear();

            mesh.vertices =
                vertices;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();
        }

        // =========================================================
        // UV
        // =========================================================

        private void BuildUVs(
            Sprite sprite)
        {
            if (sprite == null ||
                sprite.texture == null)
            {
                return;
            }

            Rect textureRect =
                sprite.textureRect;

            float textureWidth =
                sprite.texture.width;

            float textureHeight =
                sprite.texture.height;

            if (textureWidth <= 0f ||
                textureHeight <= 0f)
            {
                return;
            }

            float uMin =
                textureRect.xMin /
                textureWidth;

            float uMax =
                textureRect.xMax /
                textureWidth;

            float vMin =
                textureRect.yMin /
                textureHeight;

            float vMax =
                textureRect.yMax /
                textureHeight;

            uvs[0] =
                new Vector2(
                    uMin,
                    vMin
                );

            uvs[1] =
                new Vector2(
                    uMax,
                    vMin
                );

            uvs[2] =
                new Vector2(
                    uMax,
                    vMax
                );

            uvs[3] =
                new Vector2(
                    uMin,
                    vMax
                );
        }

        // =========================================================
        // MATERIAL
        // =========================================================

        private void UpdateMaterial(
            Texture2D texture)
        {
            if (texture == null)
                return;

            // -----------------------------------------------------
            // YA TENEMOS EL MATERIAL CORRECTO
            // -----------------------------------------------------

            if (material != null &&
                currentTexture == texture)
            {
                return;
            }

            currentTexture =
                texture;

            // -----------------------------------------------------
            // DESTRUIR MATERIAL ANTERIOR
            // -----------------------------------------------------

            if (material != null)
            {
                Destroy(
                    material
                );

                material =
                    null;
            }

            // -----------------------------------------------------
            // SHADER
            // -----------------------------------------------------

            Shader shader =
                Shader.Find(
                    "Sprites/Default"
                );

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Unlit/Transparent"
                    );
            }

            if (shader == null)
            {
                Debug.LogWarning(
                    "[PvZReanimMeshRenderer] " +
                    "No se encontró shader para renderizar " +
                    "la imagen Reanim.",
                    this
                );

                return;
            }

            // -----------------------------------------------------
            // CREAR MATERIAL
            // -----------------------------------------------------

            material =
                new Material(
                    shader
                );

            material.name =
                "PvZ Reanim Material";

            material.mainTexture =
                texture;

            meshRenderer.sharedMaterial =
                material;
        }

        // =========================================================
        // COLOR
        // =========================================================

        private void UpdateColor(
            PvZReanimTransform transformData,
            PvZReanimTrackInstance instance)
        {
            if (material == null)
                return;

            Color color =
                instance.trackColor;

            float alpha =
                transformData.GetAlpha();

            color.a *=
                Mathf.Clamp01(
                    alpha
                );

            material.color =
                color;
        }

        // =========================================================
        // SORTING
        // =========================================================

        public void SetSorting(
            int sortingLayerID,
            int sortingOrder)
        {
            Initialize();

            if (meshRenderer == null)
                return;

            meshRenderer.sortingLayerID =
                sortingLayerID;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        // =========================================================
        // VISIBILITY
        // =========================================================

        public void Hide()
        {
            if (meshRenderer == null)
            {
                meshRenderer =
                    GetComponent<MeshRenderer>();
            }

            if (meshRenderer == null)
                return;

            meshRenderer.enabled =
                false;
        }

        // =========================================================
        // SHOW
        // =========================================================

        public void Show()
        {
            if (meshRenderer == null)
            {
                meshRenderer =
                    GetComponent<MeshRenderer>();
            }

            if (meshRenderer == null)
                return;

            meshRenderer.enabled =
                true;
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(
                    mesh
                );

                mesh =
                    null;
            }

            if (material != null)
            {
                Destroy(
                    material
                );

                material =
                    null;
            }

            currentTexture =
                null;

            initialized =
                false;
        }
    }
}