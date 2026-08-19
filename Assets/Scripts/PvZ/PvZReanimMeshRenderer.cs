using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PvZReanimMeshRenderer : MonoBehaviour
    {
        // =========================================================
        // REANIM
        // =========================================================

        /*
         * PvZ trabaja en píxeles.
         *
         * El proyecto utiliza:
         *
         * 100 píxeles = 1 unidad de Unity.
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
        // MESH
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

        private Material material;

        private Texture2D currentTexture;

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
                "PvZ Reanim Mesh";

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

            BuildMesh(
                sprite,
                transformData
            );

            UpdateMaterial(
                sprite.texture
            );

            UpdateColor(
                transformData,
                instance
            );

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

            float width =
                rect.width *
                REANIM_PIXEL_TO_UNIT;

            float height =
                rect.height *
                REANIM_PIXEL_TO_UNIT;

            /*
             * =====================================================
             * IMPORTANTE
             * =====================================================
             *
             * El Reanim original NO usa Sprite.pivot.
             *
             * En el código original:
             *
             *   celWidth  = image.GetCelWidth();
             *   celHeight = image.GetCelHeight();
             *
             *   Matrix.CreateTranslation(
             *       celWidth * 0.5f,
             *       celHeight * 0.5f,
             *       0
             *   );
             *
             * Después:
             *
             *   MatrixFromTransform(...)
             *
             * y ambas matrices se multiplican.
             *
             * Por eso tenemos que construir el quad
             * desde 0,0 hasta width,height.
             */

            float halfWidth =
                width * 0.5f;

            float halfHeight =
                height * 0.5f;

            // =====================================================
            // REANIM TRANSFORM
            // =====================================================

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

            // =====================================================
            // MATRIX FROM TRANSFORM
            // =====================================================

            /*
             * Esta matriz corresponde a MatrixFromTransform()
             * del Reanim original.
             *
             * Original:
             *
             * M11 = cos(-skewX) * scaleX
             * M12 = -sin(-skewX) * scaleX
             *
             * M21 = sin(-skewY) * scaleY
             * M22 = cos(-skewY) * scaleY
             */

            float radiansX =
                -skewX *
                Mathf.Deg2Rad;

            float radiansY =
                -skewY *
                Mathf.Deg2Rad;

            float cosX =
                Mathf.Cos(
                    radiansX
                );

            float sinX =
                Mathf.Sin(
                    radiansX
                );

            float cosY =
                Mathf.Cos(
                    radiansY
                );

            float sinY =
                Mathf.Sin(
                    radiansY
                );

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

            // =====================================================
            // PIXEL -> UNITY
            // =====================================================

            float translationX =
                x *
                REANIM_PIXEL_TO_UNIT;

            /*
             * Reanim usa Y hacia abajo.
             *
             * Unity usa Y hacia arriba.
             *
             * Por eso invertimos Y.
             */
            float translationY =
                -y *
                REANIM_PIXEL_TO_UNIT;

            // =====================================================
            // QUAD ORIGINAL
            // =====================================================

            /*
             * La imagen original empieza en:
             *
             * 0,0
             *
             * y termina en:
             *
             * celWidth, celHeight
             *
             * El centro de la celda se introduce mediante:
             *
             * celWidth  * 0.5
             * celHeight * 0.5
             *
             * antes de la matriz.
             *
             * En Unity representamos eso usando un quad centrado,
             * pero aplicamos explícitamente el desplazamiento
             * transformado por la matriz.
             */

            Vector2 p0 =
                new Vector2(
                    -halfWidth,
                    -halfHeight
                );

            Vector2 p1 =
                new Vector2(
                    halfWidth,
                    -halfHeight
                );

            Vector2 p2 =
                new Vector2(
                    halfWidth,
                    halfHeight
                );

            Vector2 p3 =
                new Vector2(
                    -halfWidth,
                    halfHeight
                );

            /*
             * El desplazamiento de la mitad de la celda forma
             * parte de la transformación original de Reanim.
             *
             * Lo calculamos explícitamente.
             */

            float centerX =
                m00 * halfWidth +
                m01 * halfHeight;

            float centerY =
                m10 * halfWidth +
                m11 * halfHeight;

            /*
             * El quad centrado representa el área alrededor
             * del centro visual de la imagen.
             *
             * Sumamos el centro transformado exactamente como
             * hace GetTrackMatrix()/DrawTrack() en PvZ.
             */

            SetVertex(
                0,
                p0,
                m00,
                m01,
                m10,
                m11,
                translationX +
                centerX,
                translationY -
                centerY
            );

            SetVertex(
                1,
                p1,
                m00,
                m01,
                m10,
                m11,
                translationX +
                centerX,
                translationY -
                centerY
            );

            SetVertex(
                2,
                p2,
                m00,
                m01,
                m10,
                m11,
                translationX +
                centerX,
                translationY -
                centerY
            );

            SetVertex(
                3,
                p3,
                m00,
                m01,
                m10,
                m11,
                translationX +
                centerX,
                translationY -
                centerY
            );

            // =====================================================
            // UV
            // =====================================================

            BuildUVs(
                sprite
            );

            // =====================================================
            // APPLY MESH
            // =====================================================

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
        // SET VERTEX
        // =========================================================

        private void SetVertex(
            int index,
            Vector2 point,
            float m00,
            float m01,
            float m10,
            float m11,
            float translationX,
            float translationY)
        {
            float transformedX =
                m00 * point.x +
                m01 * point.y +
                translationX;

            float transformedY =
                m10 * point.x +
                m11 * point.y +
                translationY;

            vertices[index] =
                new Vector3(
                    transformedX,
                    transformedY,
                    0f
                );
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

            /*
             * Unity:
             *
             * abajo = vMin
             * arriba = vMax
             */

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

            if (material != null)
            {
                Destroy(
                    material
                );

                material =
                    null;
            }

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
                    "No se encontró shader.",
                    this
                );

                return;
            }

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