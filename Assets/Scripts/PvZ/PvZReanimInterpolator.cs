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

            factor = Mathf.Clamp01(factor);

            PvZReanimTransform result = a.Clone();


            result.x = InterpolateValue(
                a.x,
                b.x,
                factor,
                0f
            );

            result.y = InterpolateValue(
                a.y,
                b.y,
                factor,
                0f
            );

            result.scaleX = InterpolateValue(
                a.scaleX,
                b.scaleX,
                factor,
                1f
            );

            result.scaleY = InterpolateValue(
                a.scaleY,
                b.scaleY,
                factor,
                1f
            );

            result.alpha = InterpolateValue(
                a.alpha,
                b.alpha,
                factor,
                1f
            );

     
            float skewX1 = ResolveMissingValue(
                a.skewX,
                0f
            );

            float skewY1 = ResolveMissingValue(
                a.skewY,
                0f
            );

            float skewX2 = ResolveMissingValue(
                b.skewX,
                skewX1,
                0f
            );

            float skewY2 = ResolveMissingValue(
                b.skewY,
                skewY1,
                0f
            );

            while (skewX2 > skewX1 + 180f)
            {
                skewX2 -= 360f;
            }

            while (skewX2 < skewX1 - 180f)
            {
                skewX2 += 360f;
            }

            while (skewY2 > skewY1 + 180f)
            {
                skewY2 -= 360f;
            }

            while (skewY2 < skewY1 - 180f)
            {
                skewY2 += 360f;
            }

            result.skewX = Mathf.LerpUnclamped(
                skewX1,
                skewX2,
                factor
            );

            result.skewY = Mathf.LerpUnclamped(
                skewY1,
                skewY2,
                factor
            );

            result.frame = a.frame;


            if (!string.IsNullOrEmpty(a.imageName))
            {
                result.imageName = a.imageName;
                result.image = a.image;
            }
            else if (!string.IsNullOrEmpty(b.imageName))
            {
                result.imageName = b.imageName;
                result.image = b.image;
            }
            else
            {
                result.imageName = null;
                result.image = null;
            }

     
            if (!string.IsNullOrEmpty(a.fontName))
            {
                result.fontName = a.fontName;
            }
            else
            {
                result.fontName = b.fontName;
            }

  
            if (!string.IsNullOrEmpty(a.text))
            {
                result.text = a.text;
            }
            else
            {
                result.text = b.text;
            }

            return result;
        }

        private static float InterpolateValue(
            float a,
            float b,
            float factor,
            float defaultValue)
        {
            bool aMissing = IsMissingValue(a);
            bool bMissing = IsMissingValue(b);

            if (aMissing && bMissing)
                return defaultValue;

            if (aMissing)
                a = defaultValue;

            if (bMissing)
                b = a;

            return Mathf.LerpUnclamped(
                a,
                b,
                factor
            );
        }

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

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                PvZReanimConstants.MissingValue;
        }
    }
}