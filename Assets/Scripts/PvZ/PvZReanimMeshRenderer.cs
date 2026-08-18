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

        private Texture2D texture;

        private void Awake()
        {
            InitializeMesh();
        }

        private void InitializeMesh()
        {
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
        }

        public void Apply(
            Sprite sprite,
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (sprite == null ||
                reanimTransform == null ||
                instance == null)
            {
                meshRenderer.enabled = false;
                return;
            }

            if (mesh == null)
            {
                InitializeMesh();
            }

            BuildMesh(
                sprite,
                reanimTransform
            );

            ApplyMaterial(
                sprite
            );

            ApplyColor(
                reanimTransform,
                instance
            );

            meshRenderer.enabled =
                instance.renderGroup !=
                PvZReanimRenderGroup.Hidden;
        }

        private void BuildMesh(
            Sprite sprite,
            PvZReanimTransform reanimTransform)
        {
            Rect rect =
                sprite.rect;

            float pixelsPerUnit =
                sprite.pixelsPerUnit;

            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 100f;

            float width =
                rect.width /
                pixelsPerUnit;

            float height =
                rect.height /
                pixelsPerUnit;

            float left =
                -sprite.pivot.x /
                pixelsPerUnit;

            float right =
                left +
                width;

            float bottom =
                -sprite.pivot.y /
                pixelsPerUnit;

            float top =
                bottom +
                height;

            /*
             * Orden:
             *
             * 3 ---- 2
             * |      |
             * |      |
             * 0 ---- 1
             */

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

            ApplyReanimMatrix(
                vertices,
                reanimTransform
            );

            Rect textureRect =
                sprite.textureRect;

            float textureWidth =
                sprite.texture.width;

            float textureHeight =
                sprite.texture.height;

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

            mesh.Clear();

            mesh.vertices =
                vertices;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();
        }

        private void ApplyReanimMatrix(
            Vector3[] targetVertices,
            PvZReanimTransform reanimTransform)
        {
            PvZReanimMatrix matrix =
                PvZReanimMatrix.FromTransform(
                    reanimTransform
                );

            for (int i = 0;
                 i < targetVertices.Length;
                 i++)
            {
                targetVertices[i] =
                    matrix.MultiplyPoint(
                        targetVertices[i]
                    );
            }
        }

        private void ApplyMaterial(
            Sprite sprite)
        {
            if (sprite.texture == null)
                return;

            if (texture == sprite.texture &&
                meshRenderer.sharedMaterial != null)
            {
                return;
            }

            texture =
                sprite.texture;

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

            Material material =
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

        private void ApplyColor(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            Color color =
                instance.trackColor;

            float alpha =
                reanimTransform.alpha ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : reanimTransform.alpha;

            color.a *=
                Mathf.Clamp01(
                    alpha
                );

            if (meshRenderer.sharedMaterial != null)
            {
                meshRenderer.sharedMaterial.color =
                    color;
            }
        }

        public void SetSorting(
            int sortingLayerID,
            int sortingOrder)
        {
            if (meshRenderer == null)
            {
                meshRenderer =
                    GetComponent<MeshRenderer>();
            }

            meshRenderer.sortingLayerID =
                sortingLayerID;

            meshRenderer.sortingOrder =
                sortingOrder;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            if (meshRenderer != null &&
                meshRenderer.sharedMaterial != null)
            {
                Destroy(
                    meshRenderer.sharedMaterial
                );
            }
        }
    }
}