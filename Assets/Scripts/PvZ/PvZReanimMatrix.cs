using UnityEngine;

namespace PvZReanim
{
    public struct PvZReanimMatrix
    {
        public float m00;
        public float m01;
        public float m02;

        public float m10;
        public float m11;
        public float m12;

        public PvZReanimMatrix(
            float m00,
            float m01,
            float m02,
            float m10,
            float m11,
            float m12)
        {
            this.m00 = m00;
            this.m01 = m01;
            this.m02 = m02;

            this.m10 = m10;
            this.m11 = m11;
            this.m12 = m12;
        }

        public Vector2 MultiplyPoint(Vector2 point)
        {
            return new Vector2(
                m00 * point.x +
                m01 * point.y +
                m02,

                m10 * point.x +
                m11 * point.y +
                m12
            );
        }

        public static PvZReanimMatrix FromTransform(
            PvZReanimTransform transform)
        {
            float sx =
                transform.scaleX ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : transform.scaleX;

            float sy =
                transform.scaleY ==
                PvZReanimConstants.MissingValue
                    ? 1f
                    : transform.scaleY;

            float x =
                transform.x ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : transform.x;

            float y =
                transform.y ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : transform.y;

            float kx =
                transform.skewX ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : transform.skewX;

            float ky =
                transform.skewY ==
                PvZReanimConstants.MissingValue
                    ? 0f
                    : transform.skewY;

            float cosX = Mathf.Cos(kx);
            float sinX = Mathf.Sin(kx);

            float cosY = Mathf.Cos(ky);
            float sinY = Mathf.Sin(ky);

            return new PvZReanimMatrix(
                sx * cosX,
                -sy * sinY,
                x,

                sx * sinX,
                sy * cosY,
                y
            );
        }
    }
}