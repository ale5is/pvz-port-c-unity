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
            // Ambos faltan
            // -----------------------------------------------------

            if (aMissing && bMissing)
                return defaultValue;

            // -----------------------------------------------------
            // Solo A falta
            // -----------------------------------------------------

            if (aMissing)
                a = defaultValue;

            // -----------------------------------------------------
            // Solo B falta
            // -----------------------------------------------------

            if (bMissing)
                b = defaultValue;

            // -----------------------------------------------------
            // Interpolación lineal
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
             * Los nombres de imagen no se interpolan.
             *
             * El cambio ocurre cuando el frame pasa
             * el punto medio entre ambos keyframes.
             */

            return factor < 0.5f
                ? a
                : b;
        }
    }
}