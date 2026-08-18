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
            if (a == null)
                return b?.Clone();

            if (b == null)
                return a.Clone();

            factor = Mathf.Clamp01(factor);

            PvZReanimTransform result =
                new PvZReanimTransform();

            result.x =
                InterpolateValue(
                    a.x,
                    b.x,
                    factor
                );

            result.y =
                InterpolateValue(
                    a.y,
                    b.y,
                    factor
                );

            result.skewX =
                InterpolateValue(
                    a.skewX,
                    b.skewX,
                    factor
                );

            result.skewY =
                InterpolateValue(
                    a.skewY,
                    b.skewY,
                    factor
                );

            result.scaleX =
                InterpolateValue(
                    a.scaleX,
                    b.scaleX,
                    factor
                );

            result.scaleY =
                InterpolateValue(
                    a.scaleY,
                    b.scaleY,
                    factor
                );

            result.frame =
                InterpolateValue(
                    a.frame,
                    b.frame,
                    factor
                );

            result.alpha =
                InterpolateValue(
                    a.alpha,
                    b.alpha,
                    factor
                );

            // Los sprites no se mezclan.
            result.image =
                factor < 0.5f
                    ? a.image
                    : b.image;

            result.text =
                factor < 0.5f
                    ? a.text
                    : b.text;

            return result;
        }

        private static float InterpolateValue(
            float a,
            float b,
            float factor)
        {
            bool aMissing =
                a == PvZReanimConstants.MissingValue;

            bool bMissing =
                b == PvZReanimConstants.MissingValue;

            if (aMissing && bMissing)
                return PvZReanimConstants.MissingValue;

            if (aMissing)
                return b;

            if (bMissing)
                return a;

            return Mathf.LerpUnclamped(
                a,
                b,
                factor
            );
        }
    }
}