using UnityEngine;

namespace PvZReanim
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PvZReanimTrackRenderer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        private PvZReanimImageResolver imageResolver;

        private int sortingLayerId;

        private int sortingOrder;

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
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

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

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            // =====================================================
            // SPRITE
            // =====================================================

            Sprite sprite =
                ResolveSprite(
                    reanimTransform,
                    instance
                );

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
            // ROTATION
            // =====================================================

            ApplyRotation(
                reanimTransform
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

            // =====================================================
            // SORTING
            // =====================================================

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
            // Override
            // -----------------------------------------------------

            if (instance != null &&
                instance.imageOverride != null)
            {
                return instance.imageOverride;
            }

            // -----------------------------------------------------
            // Sprite directo
            // -----------------------------------------------------

            if (reanimTransform.image != null)
            {
                return reanimTransform.image;
            }

            // -----------------------------------------------------
            // Resolver
            // -----------------------------------------------------

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
        // ROTATION
        // =========================================================

        private void ApplyRotation(
            PvZReanimTransform reanimTransform)
        {
            /*
             * PvZ Reanim utiliza skewX/skewY.
             *
             * Unity Transform no soporta shear directamente.
             *
             * Para esta etapa mantenemos la rotación
             * aproximada utilizando la diferencia entre
             * ambos valores.
             *
             * Más adelante reemplazaremos esta parte
             * por un renderer basado en Mesh para obtener
             * shear real.
             */

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
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite =
                null;

            spriteRenderer.color =
                Color.white;

            spriteRenderer.enabled =
                false;

            transform.localPosition =
                Vector3.zero;

            transform.localRotation =
                Quaternion.identity;

            transform.localScale =
                Vector3.one;
        }
    }
}