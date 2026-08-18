using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        private PvZReanimImageResolver imageResolver;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
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

        // =========================================================
        // SORTING
        // =========================================================

        public void SetSorting(
            int sortingLayer,
            int order)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sortingLayerID =
                sortingLayer;

            spriteRenderer.sortingOrder =
                order;
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

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            // =====================================================
            // SPRITE
            // =====================================================

            Sprite sprite = null;

            if (instance != null &&
                instance.imageOverride != null)
            {
                sprite =
                    instance.imageOverride;
            }
            else
            {
                sprite =
                    ResolveSprite(
                        reanimTransform
                    );
            }

            spriteRenderer.sprite =
                sprite;

            // =====================================================
            // POSITION
            // =====================================================

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

            // =====================================================
            // SCALE
            // =====================================================

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

            // =====================================================
            // ROTATION / SKEW
            // =====================================================

            ApplySkew(
                reanimTransform.GetSkewX(),
                reanimTransform.GetSkewY()
            );

            // =====================================================
            // COLOR
            // =====================================================

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

            // =====================================================
            // VISIBILITY
            // =====================================================

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
        }

        // =========================================================
        // SPRITE RESOLUTION
        // =========================================================

        private Sprite ResolveSprite(
            PvZReanimTransform reanimTransform)
        {
            if (reanimTransform.image != null)
            {
                return reanimTransform.image;
            }

            if (imageResolver == null)
            {
                return null;
            }

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
        // SKEW
        // =========================================================

        private void ApplySkew(
            float skewX,
            float skewY)
        {
            /*
             * Unity Transform no posee shear/skew.
             *
             * Por ahora mantenemos la transformación
             * compatible con Transform.
             *
             * La representación exacta del shear se hará
             * posteriormente mediante un quad/malla propio.
             */

            float rotation =
                skewY - skewX;

            transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    rotation
                );
        }
    }
}