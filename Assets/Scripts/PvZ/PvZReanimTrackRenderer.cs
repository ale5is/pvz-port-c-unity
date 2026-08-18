using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        [Header("Renderer")]
        [SerializeField]
        private bool useMeshRenderer = true;

        private SpriteRenderer spriteRenderer;

        private PvZReanimMeshRenderer meshRenderer;

        private PvZReanimImageResolver imageResolver;

        private int sortingLayerId;

        private int sortingOrder;

        private void Awake()
        {
            InitializeRenderer();
        }

        private void InitializeRenderer()
        {
            if (useMeshRenderer)
            {
                meshRenderer =
                    GetComponent<PvZReanimMeshRenderer>();

                if (meshRenderer == null)
                {
                    meshRenderer =
                        gameObject.AddComponent<
                            PvZReanimMeshRenderer
                        >();
                }
            }
            else
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();

                if (spriteRenderer == null)
                {
                    spriteRenderer =
                        gameObject.AddComponent<
                            SpriteRenderer
                        >();
                }
            }
        }

        // =========================================================
        // IMAGE RESOLVER
        // =========================================================

        public void SetImageResolver(
            PvZReanimImageResolver newResolver)
        {
            imageResolver =
                newResolver;
        }

        public PvZReanimImageResolver GetImageResolver()
        {
            return imageResolver;
        }

        // =========================================================
        // SORTING
        // =========================================================

        public void SetSorting(
            int newSortingLayerId,
            int newSortingOrder)
        {
            sortingLayerId =
                newSortingLayerId;

            sortingOrder =
                newSortingOrder;

            ApplySorting();
        }

        private void ApplySorting()
        {
            if (useMeshRenderer)
            {
                if (meshRenderer == null)
                {
                    meshRenderer =
                        GetComponent<
                            PvZReanimMeshRenderer
                        >();
                }

                if (meshRenderer != null)
                {
                    meshRenderer.SetSorting(
                        sortingLayerId,
                        sortingOrder
                    );
                }

                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
                return;

            spriteRenderer.sortingLayerID =
                sortingLayerId;

            spriteRenderer.sortingOrder =
                sortingOrder;
        }

        // =========================================================
        // APPLY
        // =========================================================

        public void Apply(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (reanimTransform == null)
                return;

            InitializeRenderer();

            Sprite sprite =
                ResolveSprite(
                    reanimTransform,
                    instance
                );

            if (useMeshRenderer)
            {
                ApplyMesh(
                    sprite,
                    reanimTransform,
                    instance
                );

                return;
            }

            ApplySprite(
                sprite,
                reanimTransform,
                instance
            );
        }

        // =========================================================
        // MESH
        // =========================================================

        private void ApplyMesh(
            Sprite sprite,
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (meshRenderer == null)
                return;

            if (sprite == null ||
                instance == null)
            {
                meshRenderer.Hide();
                return;
            }

            meshRenderer.Apply(
                sprite,
                reanimTransform,
                instance
            );

            ApplySorting();
        }

        // =========================================================
        // SPRITE
        // =========================================================

        private void ApplySprite(
            Sprite sprite,
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();

                if (spriteRenderer == null)
                {
                    spriteRenderer =
                        gameObject.AddComponent<
                            SpriteRenderer
                        >();
                }
            }

            spriteRenderer.sprite =
                sprite;

            float x =
                reanimTransform.GetX();

            float y =
                reanimTransform.GetY();

            transform.localPosition =
                new Vector3(
                    x,
                    y,
                    0f
                );

            float scaleX =
                reanimTransform.GetScaleX();

            float scaleY =
                reanimTransform.GetScaleY();

            transform.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f
                );

            ApplyRotation(
                reanimTransform
            );

            Color color =
                instance != null
                    ? instance.trackColor
                    : Color.white;

            float alpha =
                reanimTransform.GetAlpha();

            color.a *=
                Mathf.Clamp01(
                    alpha
                );

            spriteRenderer.color =
                color;

            bool visible =
                sprite != null;

            if (instance != null)
            {
                visible &=
                    instance.renderGroup !=
                    PvZReanimRenderGroup.Hidden;
            }

            spriteRenderer.enabled =
                visible;

            ApplySorting();
        }

        // =========================================================
        // SPRITE RESOLUTION
        // =========================================================

        private Sprite ResolveSprite(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (instance != null &&
                instance.imageOverride != null)
            {
                return instance.imageOverride;
            }

            if (reanimTransform.image != null)
            {
                return reanimTransform.image;
            }

            if (imageResolver == null)
                return null;

            if (string.IsNullOrEmpty(
                reanimTransform.imageName))
            {
                return null;
            }

            return imageResolver.Resolve(
                reanimTransform.imageName
            );
        }

        // =========================================================
        // ROTATION FALLBACK
        // =========================================================

        private void ApplyRotation(
            PvZReanimTransform reanimTransform)
        {
            float skewX =
                reanimTransform.GetSkewX();

            float skewY =
                reanimTransform.GetSkewY();

            float rotation =
                skewY - skewX;

            transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    rotation
                );
        }

        // =========================================================
        // RESET
        // =========================================================

        public void ResetRenderer()
        {
            InitializeRenderer();

            if (meshRenderer != null)
            {
                meshRenderer.Hide();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite =
                    null;

                spriteRenderer.color =
                    Color.white;

                spriteRenderer.enabled =
                    false;
            }

            transform.localPosition =
                Vector3.zero;

            transform.localRotation =
                Quaternion.identity;

            transform.localScale =
                Vector3.one;
        }
    }
}