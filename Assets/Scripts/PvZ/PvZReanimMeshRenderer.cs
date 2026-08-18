using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PvZReanimMeshRenderer : MonoBehaviour
    {
        private MeshFilter meshFilter;

        private MeshRenderer meshRenderer;

        private Mesh mesh;

        private Vector3[] vertices;

        private Vector2[] uvs;

        private int[] triangles;

        private Texture2D currentTexture;

        private Material material;

        private bool initialized;

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

            vertices =
                new Vector3[4];

            uvs =
                new Vector2[4];

            triangles =
                new int[]
                {
                    0, 1, 2,
                    2, 3, 0
                };

            initialized = true;
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
            if (sprite == null)
                return;

            float pixelsPerUnit =
                sprite.pixelsPerUnit;

            if (pixelsPerUnit <= 0f)
            {
                pixelsPerUnit =
                    100f;
            }

            Rect rect =
                sprite.rect;

            float width =
                rect.width /
                pixelsPerUnit;

            float height =
                rect.height /
                pixelsPerUnit;

            Vector2 pivot =
                sprite.pivot;

            float left =
                -pivot.x /
                pixelsPerUnit;

            float bottom =
                -pivot.y /
                pixelsPerUnit;

            float right =
                left +
                width;

            float top =
                bottom +
                height;

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

            PvZReanimMatrix matrix =
                PvZReanimMatrix.FromTransform(
                    transformData
                );

            for (int i = 0;
                 i < vertices.Length;
                 i++)
            {
                vertices[i] =
                    matrix.MultiplyPoint(
                        vertices[i]
                    );
            }

            BuildUVs(
                sprite
            );

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

            if (material != null)
            {
                Destroy(
                    material
                );

                material = null;
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
                return;

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

                mesh = null;
            }

            if (material != null)
            {
                Destroy(
                    material
                );

                material = null;
            }
        }
    }
}