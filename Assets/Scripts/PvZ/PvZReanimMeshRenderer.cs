using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PvZReanimMeshRenderer :
        MonoBehaviour
    {
        private MeshFilter meshFilter;

        private MeshRenderer meshRenderer;

        private Mesh mesh;

        private Vector3[] vertices;

        private Vector2[] uvs;

        private int[] triangles;

        private Texture2D currentTexture;

        private Material material;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
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
            PvZReanimTransform transform,
            PvZReanimTrackInstance instance)
        {
            if (sprite == null ||
                transform == null ||
                instance == null)
            {
                Hide();
                return;
            }

            if (mesh == null)
            {
                Initialize();
            }

            BuildMesh(
                sprite,
                transform
            );

            UpdateMaterial(
                sprite.texture
            );

            UpdateColor(
                transform,
                instance
            );

            meshRenderer.enabled =
                instance.renderGroup !=
                PvZReanimRenderGroup.Hidden;
        }

        private void BuildMesh(
            Sprite sprite,
            PvZReanimTransform transform)
        {
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
                    transform
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

            mesh.Clear();

            mesh.vertices =
                vertices;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();
        }

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

        private void UpdateColor(
            PvZReanimTransform transform,
            PvZReanimTrackInstance instance)
        {
            if (material == null)
                return;

            Color color =
                instance.trackColor;

            float alpha =
                transform.alpha ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : transform.alpha;

            color.a *=
                Mathf.Clamp01(
                    alpha
                );

            material.color =
                color;
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

        public void Hide()
        {
            if (meshRenderer == null)
                return;

            meshRenderer.enabled =
                false;
        }

        public void Show()
        {
            if (meshRenderer == null)
                return;

            meshRenderer.enabled =
                true;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(
                    mesh
                );
            }

            if (material != null)
            {
                Destroy(
                    material
                );
            }
        }
    }
}