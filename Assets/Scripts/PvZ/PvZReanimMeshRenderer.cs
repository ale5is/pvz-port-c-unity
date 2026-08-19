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
         * PvZ/Reanim trabaja en píxeles.
         *
         * Usamos:
         *
         * 100 píxeles = 1 unidad de Unity
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
        // INITIALIZE
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
            // MESH
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
            // VISIBILITY
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

            // -----------------------------------------------------
            // IMAGE SIZE
            // -----------------------------------------------------

            float width =
                rect.width *
                REANIM_PIXEL_TO_UNIT;

            float height =
                rect.height *
                REANIM_PIXEL_TO_UNIT;

            // -----------------------------------------------------
            // PIVOT
            //
            // Reanim coloca la imagen alrededor de su centro.
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
            // VALORES REANIM
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

            // -----------------------------------------------------
            // MATRIZ REANIM ORIGINAL
            //
            // Esta es la misma estructura utilizada por
            // PlantsVsZombies.NET / Reanim original:
            //
            // M11 = cos(skewX) * scaleX
            // M12 = -sin(skewX) * scaleX
            // M21 = sin(skewY) * scaleY
            // M22 = cos(skewY) * scaleY
            //
            // IMPORTANTE:
            // skewX y skewY NO se pueden intercambiar.
            // -----------------------------------------------------

            float skewXRadians =
                -skewX *
                Mathf.Deg2Rad;

            float skewYRadians =
                -skewY *
                Mathf.Deg2Rad;

            float cosX =
                Mathf.Cos(
                    skewXRadians
                );

            float sinX =
                Mathf.Sin(
                    skewXRadians
                );

            float cosY =
                Mathf.Cos(
                    skewYRadians
                );

            float sinY =
                Mathf.Sin(
                    skewYRadians
                );

            // -----------------------------------------------------
            // MATRIZ CORRECTA DE REANIM
            // -----------------------------------------------------

            float m00 =
                cosX *
                scaleX;

            float m01 =
                -sinX *
                scaleX;

            float m10 =
                sinY *
                scaleY;

            float m11 =
                cosY *
                scaleY;

            // -----------------------------------------------------
            // TRANSLACIÓN
            // -----------------------------------------------------

            float translationX =
                x *
                REANIM_PIXEL_TO_UNIT;

            float translationY =
                y *
                REANIM_PIXEL_TO_UNIT;

            // -----------------------------------------------------
            // VERTICES BASE
            //
            // El quad está centrado usando el pivot real del Sprite.
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
            // APLICAR MATRIZ
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

            if (material != null &&
                currentTexture == texture)
            {
                return;
            }

            currentTexture =
                texture;

            // -----------------------------------------------------
            // MATERIAL ANTERIOR
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
        // HIDE
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