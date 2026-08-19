using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimInterpolator
    {
        // =========================================================
        // INTERPOLAR
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
            // SKEW
            //
            // Resodded evita saltos de 360 grados.
            // =====================================================

            float skewX2 =
                ResolveMissingValue(
                    b.skewX,
                    a.skewX,
                    0f
                );

            float skewY2 =
                ResolveMissingValue(
                    b.skewY,
                    a.skewY,
                    0f
                );

            float skewX1 =
                ResolveMissingValue(
                    a.skewX,
                    0f
                );

            float skewY1 =
                ResolveMissingValue(
                    a.skewY,
                    0f
                );

            while (skewX2 >
                   skewX1 + 180f)
            {
                skewX2 =
                    skewX1;
            }

            while (skewX2 <
                   skewX1 - 180f)
            {
                skewX2 =
                    skewX1;
            }

            while (skewY2 >
                   skewY1 + 180f)
            {
                skewY2 =
                    skewY1;
            }

            while (skewY2 <
                   skewY1 - 180f)
            {
                skewY2 =
                    skewY1;
            }

            result.skewX =
                Mathf.LerpUnclamped(
                    skewX1,
                    skewX2,
                    factor
                );

            result.skewY =
                Mathf.LerpUnclamped(
                    skewY1,
                    skewY2,
                    factor
                );

            // =====================================================
            // FRAME
            //
            // MUY IMPORTANTE:
            //
            // Resodded NO interpola el frame.
            // Utiliza el frame anterior.
            // =====================================================

            result.frame =
                a.frame;

            // =====================================================
            // IMAGE
            //
            // También es un valor discreto.
            // Se conserva el valor del frame anterior.
            // =====================================================

            result.imageName =
                a.imageName;

            result.image =
                a.image;

            // =====================================================
            // FONT
            // =====================================================

            result.fontName =
                a.fontName;

            // =====================================================
            // TEXT
            // =====================================================

            result.text =
                a.text;

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
                IsMissingValue(a);

            bool bMissing =
                IsMissingValue(b);

            if (aMissing &&
                bMissing)
            {
                return defaultValue;
            }

            if (aMissing)
            {
                a =
                    defaultValue;
            }

            if (bMissing)
            {
                b =
                    a;
            }

            return Mathf.LerpUnclamped(
                a,
                b,
                factor
            );
        }

        // =========================================================
        // RESOLVER VALUE
        // =========================================================

        private static float ResolveMissingValue(
            float value,
            float fallback)
        {
            return IsMissingValue(value)
                ? fallback
                : value;
        }

        private static float ResolveMissingValue(
            float value,
            float fallback1,
            float fallback2)
        {
            if (!IsMissingValue(value))
                return value;

            if (!IsMissingValue(fallback1))
                return fallback1;

            return fallback2;
        }

        // =========================================================
        // MISSING
        // =========================================================

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                PvZReanimConstants.MissingValue;
        }
    }
}