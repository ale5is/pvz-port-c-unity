using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimInterpolator
    {
        // =========================================================
        // MAIN
        // =========================================================

        public static PvZReanimTransform Interpolate(
            PvZReanimTransform a,
            PvZReanimTransform b,
            float factor)
        {
            if (a == null && b == null)
                return null;

            if (a == null)
                return b.Clone();

            if (b == null)
                return a.Clone();

            factor =
                Mathf.Clamp01(
                    factor
                );

            PvZReanimTransform result =
                a.Clone();

            // =====================================================
            // POSITION
            // =====================================================

            result.x =
                InterpolateValue(
                    a.x,
                    b.x,
                    factor,
                    0f
                );

            result.y =
                InterpolateValue(
                    a.y,
                    b.y,
                    factor,
                    0f
                );

            // =====================================================
            // SKEW
            // =====================================================

            result.skewX =
                InterpolateValue(
                    a.skewX,
                    b.skewX,
                    factor,
                    0f
                );

            result.skewY =
                InterpolateValue(
                    a.skewY,
                    b.skewY,
                    factor,
                    0f
                );

            // =====================================================
            // SCALE
            // =====================================================

            result.scaleX =
                InterpolateValue(
                    a.scaleX,
                    b.scaleX,
                    factor,
                    1f
                );

            result.scaleY =
                InterpolateValue(
                    a.scaleY,
                    b.scaleY,
                    factor,
                    1f
                );

            // =====================================================
            // FRAME
            // =====================================================

            result.frame =
                InterpolateValue(
                    a.frame,
                    b.frame,
                    factor,
                    0f
                );

            // =====================================================
            // ALPHA
            // =====================================================

            result.alpha =
                InterpolateValue(
                    a.alpha,
                    b.alpha,
                    factor,
                    1f
                );

            // =====================================================
            // IMAGE
            // =====================================================

            result.imageName =
                ResolveImageName(
                    a.imageName,
                    b.imageName,
                    factor
                );

            // =====================================================
            // TEXT
            // =====================================================

            result.text =
                ResolveText(
                    a.text,
                    b.text,
                    factor
                );

            // =====================================================
            // SPRITE
            // =====================================================

            result.image =
                ResolveSprite(
                    a.image,
                    b.image,
                    factor
                );

            return result;
        }

        // =========================================================
        // FLOAT
        // =========================================================

        private static float InterpolateValue(
            float a,
            float b,
            float factor,
            float defaultValue)
        {
            bool aMissing =
                a ==
                PvZReanimConstants.MissingValue;

            bool bMissing =
                b ==
                PvZReanimConstants.MissingValue;

            // -----------------------------------------------------
            // Ambos inexistentes
            // -----------------------------------------------------

            if (aMissing && bMissing)
                return defaultValue;

            // -----------------------------------------------------
            // A inexistente
            // -----------------------------------------------------

            if (aMissing)
                a = defaultValue;

            // -----------------------------------------------------
            // B inexistente
            // -----------------------------------------------------

            if (bMissing)
                b = defaultValue;

            // -----------------------------------------------------
            // Linear interpolation
            // -----------------------------------------------------

            return Mathf.LerpUnclamped(
                a,
                b,
                factor
            );
        }

        // =========================================================
        // IMAGE NAME
        // =========================================================

        private static string ResolveImageName(
            string a,
            string b,
            float factor)
        {
            bool aValid =
                !string.IsNullOrEmpty(
                    a
                );

            bool bValid =
                !string.IsNullOrEmpty(
                    b
                );

            if (!aValid && !bValid)
                return null;

            if (!aValid)
                return b;

            if (!bValid)
                return a;

            /*
             * imageName es un valor discreto.
             *
             * No se interpola como float.
             */

            return factor < 0.5f
                ? a
                : b;
        }

        // =========================================================
        // TEXT
        // =========================================================

        private static string ResolveText(
            string a,
            string b,
            float factor)
        {
            bool aValid =
                !string.IsNullOrEmpty(
                    a
                );

            bool bValid =
                !string.IsNullOrEmpty(
                    b
                );

            if (!aValid && !bValid)
                return null;

            if (!aValid)
                return b;

            if (!bValid)
                return a;

            return factor < 0.5f
                ? a
                : b;
        }

        // =========================================================
        // SPRITE
        // =========================================================

        private static Sprite ResolveSprite(
            Sprite a,
            Sprite b,
            float factor)
        {
            if (a == null && b == null)
                return null;

            if (a == null)
                return b;

            if (b == null)
                return a;

            return factor < 0.5f
                ? a
                : b;
        }
    }
}