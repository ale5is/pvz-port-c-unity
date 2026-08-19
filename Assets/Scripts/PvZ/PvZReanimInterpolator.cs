using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimInterpolator
    {
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
                Mathf.Clamp01(factor);

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
            // =====================================================

            float skewX1 =
                ResolveMissing(
                    a.skewX,
                    0f
                );

            float skewY1 =
                ResolveMissing(
                    a.skewY,
                    0f
                );

            float skewX2 =
                ResolveMissing(
                    b.skewX,
                    skewX1
                );

            float skewY2 =
                ResolveMissing(
                    b.skewY,
                    skewY1
                );

            while (skewX2 >
                   skewX1 + 180f)
            {
                skewX2 -= 360f;
            }

            while (skewX2 <
                   skewX1 - 180f)
            {
                skewX2 += 360f;
            }

            while (skewY2 >
                   skewY1 + 180f)
            {
                skewY2 -= 360f;
            }

            while (skewY2 <
                   skewY1 - 180f)
            {
                skewY2 += 360f;
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
            // =====================================================

            /*
             * El frame es discreto.
             *
             * Mientras estamos entre dos transformaciones,
             * utilizamos el frame de la transformación anterior.
             */
            result.frame =
                !IsMissing(a.frame)
                    ? a.frame
                    : b.frame;

            // =====================================================
            // IMAGE
            // =====================================================

            /*
             * MUY IMPORTANTE:
             *
             * Si el frame anterior tiene imagen, se conserva.
             * Si no tiene imagen, usamos la del siguiente.
             *
             * Esto evita que la pieza desaparezca al cambiar
             * de frame solamente porque imageName sea MissingValue.
             */
            if (!string.IsNullOrEmpty(a.imageName))
            {
                result.imageName =
                    a.imageName;

                result.image =
                    a.image;
            }
            else
            {
                result.imageName =
                    b.imageName;

                result.image =
                    b.image;
            }

            // =====================================================
            // FONT
            // =====================================================

            result.fontName =
                !string.IsNullOrEmpty(a.fontName)
                    ? a.fontName
                    : b.fontName;

            // =====================================================
            // TEXT
            // =====================================================

            result.text =
                !string.IsNullOrEmpty(a.text)
                    ? a.text
                    : b.text;

            return result;
        }

        private static float InterpolateValue(
            float a,
            float b,
            float factor,
            float defaultValue)
        {
            bool aMissing =
                IsMissing(a);

            bool bMissing =
                IsMissing(b);

            if (aMissing && bMissing)
                return defaultValue;

            if (aMissing)
                a = b;

            if (bMissing)
                b = a;

            return Mathf.LerpUnclamped(
                a,
                b,
                factor
            );
        }

        private static float ResolveMissing(
            float value,
            float fallback)
        {
            return IsMissing(value)
                ? fallback
                : value;
        }

        private static bool IsMissing(
            float value)
        {
            return value ==
                PvZReanimConstants.MissingValue;
        }
    }
}