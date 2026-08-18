using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        private PvZReanimImageResolver imageResolver;

        private string currentImageName;

        private Sprite currentSprite;

        private int trackIndex;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        // =========================================================
        // CONFIGURATION
        // =========================================================

        public void SetImageResolver(
            PvZReanimImageResolver resolver)
        {
            imageResolver =
                resolver;

            currentImageName = null;
            currentSprite = null;
        }

        public void SetTrackIndex(
            int index)
        {
            trackIndex =
                index;
        }

        public int TrackIndex =>
            trackIndex;

        // =========================================================
        // APPLY
        // =========================================================

        public void Apply(
            PvZReanimTransform reanimTransform,
            PvZReanimTrackInstance instance)
        {
            if (reanimTransform == null ||
                instance == null)
            {
                Hide();
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            // =====================================================
            // SPRITE
            // =====================================================

            Sprite sprite =
                instance.imageOverride;

            if (sprite == null)
            {
                sprite =
                    ResolveImage(
                        reanimTransform.imageName
                    );
            }

            currentSprite =
                sprite;

            // =====================================================
            // POSITION
            // =====================================================

            float x =
                GetValue(
                    reanimTransform.x,
                    0f
                );

            float y =
                GetValue(
                    reanimTransform.y,
                    0f
                );

            transform.localPosition =
                new Vector3(
                    x,
                    y,
                    0f
                );

            // =====================================================
            // SCALE
            // =====================================================

            float scaleX =
                GetValue(
                    reanimTransform.scaleX,
                    1f
                );

            float scaleY =
                GetValue(
                    reanimTransform.scaleY,
                    1f
                );

            transform.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f
                );

            // =====================================================
            // ALPHA
            // =====================================================

            Color color =
                instance.trackColor;

            float alpha =
                GetValue(
                    reanimTransform.alpha,
                    1f
                );

            color.a *= alpha;

            spriteRenderer.color =
                color;

            // =====================================================
            // SPRITE
            // =====================================================

            spriteRenderer.sprite =
                sprite;

            // =====================================================
            // VISIBILITY
            // =====================================================

            bool visible =
                sprite != null &&
                instance.renderGroup !=
                PvZReanimRenderGroup.Hidden;

            spriteRenderer.enabled =
                visible;
        }

        // =========================================================
        // IMAGE RESOLUTION
        // =========================================================

        private Sprite ResolveImage(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                imageName))
            {
                return null;
            }

            if (imageResolver == null)
            {
                return null;
            }

            string normalized =
                PvZReanimImageResolver
                    .NormalizeName(
                        imageName
                    );

            if (string.Equals(
                currentImageName,
                normalized,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return currentSprite;
            }

            currentImageName =
                normalized;

            return imageResolver.Resolve(
                imageName
            );
        }

        // =========================================================
        // VALUE
        // =========================================================

        private static float GetValue(
            float value,
            float defaultValue)
        {
            return value ==
                   PvZReanimConstants.MissingValue
                ? defaultValue
                : value;
        }

        // =========================================================
        // VISIBILITY
        // =========================================================

        public void Hide()
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.enabled =
                false;
        }

        public void Show()
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.enabled =
                currentSprite != null;
        }

        // =========================================================
        // CURRENT IMAGE
        // =========================================================

        public Sprite CurrentSprite =>
            currentSprite;

        public string CurrentImageName =>
            currentImageName;

        // =========================================================
        // SORTING
        // =========================================================

        public void SetSorting(
            int sortingLayerId,
            int order)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingLayerID =
                sortingLayerId;

            spriteRenderer.sortingOrder =
                order;
        }

        public void SetSortingLayer(
            string sortingLayer)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingLayerName =
                sortingLayer;
        }

        public void SetSortingOrder(
            int order)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingOrder =
                order;
        }
    }
}