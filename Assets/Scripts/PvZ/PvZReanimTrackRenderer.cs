using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        [Header("Renderer")]
        [SerializeField]
        private bool useMeshRenderer = true;

        [Header("Debug")]
        [SerializeField]
        private bool logMissingSprites = true;

        private SpriteRenderer spriteRenderer;

        private PvZReanimMeshRenderer meshRenderer;

        private PvZReanimImageResolver imageResolver;

        private int sortingLayerId;

        private int sortingOrder;

        private string lastMissingImage;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            InitializeRenderer();
        }

        // =========================================================
        // INITIALIZE
        // =========================================================

        private void InitializeRenderer()
        {
            if (useMeshRenderer)
            {
                meshRenderer =
                    GetComponent<
                        PvZReanimMeshRenderer
                    >();

                if (meshRenderer == null)
                {
                    meshRenderer =
                        gameObject.AddComponent<
                            PvZReanimMeshRenderer
                        >();
                }

                return;
            }

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
            {
                Hide();
                return;
            }

            InitializeRenderer();

            // -----------------------------------------------------
            // FRAME VISIBILITY
            // -----------------------------------------------------

            if (!IsFrameVisible(
                    reanimTransform))
            {
                Hide();
                return;
            }

            // -----------------------------------------------------
            // RENDER GROUP
            // -----------------------------------------------------

            if (instance != null &&
                instance.renderGroup ==
                PvZReanimRenderGroup.Hidden)
            {
                Hide();
                return;
            }

            // -----------------------------------------------------
            // RESOLVE SPRITE
            // -----------------------------------------------------

            Sprite sprite =
                ResolveSprite(
                    reanimTransform,
                    instance
                );

            if (sprite == null)
            {
                Hide();

                LogMissingSprite(
                    reanimTransform
                );

                return;
            }

            // -----------------------------------------------------
            // APPLY
            // -----------------------------------------------------

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
        // FRAME VISIBILITY
        // =========================================================

        private bool IsFrameVisible(
            PvZReanimTransform reanimTransform)
        {
            if (reanimTransform == null)
                return false;

            if (!reanimTransform.HasFrame)
                return true;

            float frame =
                reanimTransform.GetFrame();

            // En Reanim, un frame negativo
            // representa un elemento que no debe
            // renderizarse en ese momento.
            if (frame < 0f)
                return false;

            return true;
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

            if (sprite == null)
            {
                meshRenderer.Hide();
                return;
            }

            if (instance == null)
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
        // SPRITE RENDERER
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

            // -----------------------------------------------------
            // POSITION
            // -----------------------------------------------------

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

            // -----------------------------------------------------
            // SCALE
            // -----------------------------------------------------

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

            // -----------------------------------------------------
            // ROTATION
            // -----------------------------------------------------

            ApplyRotation(
                reanimTransform
            );

            // -----------------------------------------------------
            // COLOR
            // -----------------------------------------------------

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

            // -----------------------------------------------------
            // VISIBILITY
            // -----------------------------------------------------

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
            // -----------------------------------------------------
            // 1. OVERRIDE
            // -----------------------------------------------------

            if (instance != null &&
                instance.imageOverride != null)
            {
                return instance.imageOverride;
            }

            // -----------------------------------------------------
            // 2. IMAGE DIRECTA
            // -----------------------------------------------------

            if (reanimTransform.image != null)
            {
                return reanimTransform.image;
            }

            // -----------------------------------------------------
            // 3. RESOLVER
            // -----------------------------------------------------

            if (imageResolver == null)
            {
                imageResolver =
                    GetComponent<
                        PvZReanimImageResolver
                    >();

                if (imageResolver == null)
                {
                    imageResolver =
                        GetComponentInParent<
                            PvZReanimImageResolver
                        >();
                }

                if (imageResolver == null)
                {
                    imageResolver =
                        FindFirstObjectByType<
                            PvZReanimImageResolver
                        >();
                }
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
        // ROTATION
        // =========================================================

        private void ApplyRotation(
            PvZReanimTransform reanimTransform)
        {
            if (reanimTransform == null)
                return;

            float skewX =
                reanimTransform.GetSkewX();

            float skewY =
                reanimTransform.GetSkewY();

            float rotation =
                skewY -
                skewX;

            transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    rotation
                );
        }

        // =========================================================
        // HIDE
        // =========================================================

        private void Hide()
        {
            InitializeRenderer();

            if (meshRenderer != null)
            {
                meshRenderer.Hide();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled =
                    false;
            }
        }

        // =========================================================
        // MISSING DEBUG
        // =========================================================

        private void LogMissingSprite(
            PvZReanimTransform reanimTransform)
        {
            if (!logMissingSprites)
                return;

            if (reanimTransform == null)
                return;

            string imageName =
                reanimTransform.imageName;

            if (string.IsNullOrEmpty(
                    imageName))
            {
                return;
            }

            if (string.Equals(
                    lastMissingImage,
                    imageName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lastMissingImage =
                imageName;

            Debug.LogWarning(
                "[PvZReanimTrackRenderer] " +
                "No se pudo resolver la imagen: " +
                imageName,
                this
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

            lastMissingImage =
                null;
        }
    }
}