using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Matriz 2D utilizada por Reanim.
    ///
    /// Equivalente a:
    ///
    /// m00 = cos(-skewX) * scaleX
    /// m10 = -sin(-skewX) * scaleX
    /// m01 = sin(-skewY) * scaleY
    /// m11 = cos(-skewY) * scaleY
    ///
    /// siguiendo MatrixFromTransform() de Resodded.
    /// </summary>
    [System.Serializable]
    public struct PvZReanimMatrix
    {
        public float m00;
        public float m01;
        public float m02;

        public float m10;
        public float m11;
        public float m12;

        public float m20;
        public float m21;
        public float m22;

        public PvZReanimMatrix(
            float m00,
            float m01,
            float m02,
            float m10,
            float m11,
            float m12,
            float m20,
            float m21,
            float m22)
        {
            this.m00 = m00;
            this.m01 = m01;
            this.m02 = m02;

            this.m10 = m10;
            this.m11 = m11;
            this.m12 = m12;

            this.m20 = m20;
            this.m21 = m21;
            this.m22 = m22;
        }

        public static PvZReanimMatrix Identity
        {
            get
            {
                return new PvZReanimMatrix(
                    1f, 0f, 0f,
                    0f, 1f, 0f,
                    0f, 0f, 1f
                );
            }
        }

        // =========================================================
        // CREAR MATRIZ DESDE REANIM
        // =========================================================

        public static PvZReanimMatrix FromTransform(
            PvZReanimTransform transform)
        {
            if (transform == null)
                return Identity;

            float x =
                GetValue(
                    transform.x,
                    0f
                );

            float y =
                GetValue(
                    transform.y,
                    0f
                );

            float skewX =
                GetValue(
                    transform.skewX,
                    0f
                );

            float skewY =
                GetValue(
                    transform.skewY,
                    0f
                );

            float scaleX =
                GetValue(
                    transform.scaleX,
                    1f
                );

            float scaleY =
                GetValue(
                    transform.scaleY,
                    1f
                );

            /*
             * Esto es lo que hace Resodded:
             *
             * float aSkewX = -DEG_TO_RAD(mSkewX);
             * float aSkewY = -DEG_TO_RAD(mSkewY);
             *
             * m00 = cos(aSkewX) * scaleX;
             * m10 = -sin(aSkewX) * scaleX;
             *
             * m01 = sin(aSkewY) * scaleY;
             * m11 = cos(aSkewY) * scaleY;
             */

            float radiansX =
                -skewX *
                Mathf.Deg2Rad;

            float radiansY =
                -skewY *
                Mathf.Deg2Rad;

            float cosX =
                Mathf.Cos(radiansX);

            float sinX =
                Mathf.Sin(radiansX);

            float cosY =
                Mathf.Cos(radiansY);

            float sinY =
                Mathf.Sin(radiansY);

            return new PvZReanimMatrix(
                cosX * scaleX,
                sinY * scaleY,
                x,

                -sinX * scaleX,
                cosY * scaleY,
                y,

                0f,
                0f,
                1f
            );
        }

        // =========================================================
        // MULTIPLICAR PUNTO
        // =========================================================

        public Vector2 MultiplyPoint(
            Vector2 point)
        {
            float x =
                m00 * point.x +
                m01 * point.y +
                m02;

            float y =
                m10 * point.x +
                m11 * point.y +
                m12;

            return new Vector2(
                x,
                y
            );
        }

        public Vector3 MultiplyPoint(
            Vector3 point)
        {
            Vector2 result =
                MultiplyPoint(
                    new Vector2(
                        point.x,
                        point.y
                    )
                );

            return new Vector3(
                result.x,
                result.y,
                point.z
            );
        }

        // =========================================================
        // MULTIPLICAR MATRICES
        // =========================================================

        public static PvZReanimMatrix Multiply(
            PvZReanimMatrix a,
            PvZReanimMatrix b)
        {
            return new PvZReanimMatrix(
                a.m00 * b.m00 +
                a.m01 * b.m10 +
                a.m02 * b.m20,

                a.m00 * b.m01 +
                a.m01 * b.m11 +
                a.m02 * b.m21,

                a.m00 * b.m02 +
                a.m01 * b.m12 +
                a.m02 * b.m22,

                a.m10 * b.m00 +
                a.m11 * b.m10 +
                a.m12 * b.m20,

                a.m10 * b.m01 +
                a.m11 * b.m11 +
                a.m12 * b.m21,

                a.m10 * b.m02 +
                a.m11 * b.m12 +
                a.m12 * b.m22,

                a.m20 * b.m00 +
                a.m21 * b.m10 +
                a.m22 * b.m20,

                a.m20 * b.m01 +
                a.m21 * b.m11 +
                a.m22 * b.m21,

                a.m20 * b.m02 +
                a.m21 * b.m12 +
                a.m22 * b.m22
            );
        }

        // =========================================================
        // UNITY
        // =========================================================

        public Matrix4x4 ToUnityMatrix()
        {
            Matrix4x4 result =
                Matrix4x4.identity;

            result.m00 = m00;
            result.m01 = m01;
            result.m03 = m02;

            result.m10 = m10;
            result.m11 = m11;
            result.m13 = m12;

            result.m20 = m20;
            result.m21 = m21;
            result.m22 = m22;

            return result;
        }

        // =========================================================
        // POSITION
        // =========================================================

        public Vector3 GetPosition()
        {
            return new Vector3(
                m02,
                m12,
                0f
            );
        }

        // =========================================================
        // LERP
        // =========================================================

        public static PvZReanimMatrix Lerp(
            PvZReanimMatrix a,
            PvZReanimMatrix b,
            float factor)
        {
            factor =
                Mathf.Clamp01(
                    factor
                );

            return new PvZReanimMatrix(
                Mathf.Lerp(
                    a.m00,
                    b.m00,
                    factor
                ),

                Mathf.Lerp(
                    a.m01,
                    b.m01,
                    factor
                ),

                Mathf.Lerp(
                    a.m02,
                    b.m02,
                    factor
                ),

                Mathf.Lerp(
                    a.m10,
                    b.m10,
                    factor
                ),

                Mathf.Lerp(
                    a.m11,
                    b.m11,
                    factor
                ),

                Mathf.Lerp(
                    a.m12,
                    b.m12,
                    factor
                ),

                Mathf.Lerp(
                    a.m20,
                    b.m20,
                    factor
                ),

                Mathf.Lerp(
                    a.m21,
                    b.m21,
                    factor
                ),

                Mathf.Lerp(
                    a.m22,
                    b.m22,
                    factor
                )
            );
        }

        private static float GetValue(
            float value,
            float fallback)
        {
            return value ==
                   PvZReanimConstants.MissingValue
                ? fallback
                : value;
        }

        public override string ToString()
        {
            return
                $"[{m00}, {m01}, {m02}] " +
                $"[{m10}, {m11}, {m12}] " +
                $"[{m20}, {m21}, {m22}]";
        }
    }
}