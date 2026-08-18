using UnityEngine;

namespace PvZReanim
{
    /// <summary>
    /// Matriz 2D utilizada para representar la transformación
    /// de un track de Reanim.
    ///
    /// Mantiene separadas:
    /// - posición
    /// - escala
    /// - skew
    ///
    /// para que el renderer pueda aplicar posteriormente
    /// la transformación completa.
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

        public static PvZReanimMatrix Identity
        {
            get
            {
                PvZReanimMatrix matrix =
                    new PvZReanimMatrix();

                matrix.m00 = 1f;
                matrix.m01 = 0f;
                matrix.m02 = 0f;

                matrix.m10 = 0f;
                matrix.m11 = 1f;
                matrix.m12 = 0f;

                matrix.m20 = 0f;
                matrix.m21 = 0f;
                matrix.m22 = 1f;

                return matrix;
            }
        }

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

        /// <summary>
        /// Crea una matriz a partir de un transform de Reanim.
        /// </summary>
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

            /*
             * Reanim utiliza valores de skew para construir
             * una transformación 2D.
             *
             * Convertimos los ángulos a radianes.
             */
            float skewXRadians =
                skewX *
                Mathf.Deg2Rad;

            float skewYRadians =
                skewY *
                Mathf.Deg2Rad;

            /*
             * Construcción de la base 2D.
             *
             * Los términos de skew generan una matriz
             * afín que puede representar inclinación sin
             * convertirla simplemente en rotación.
             */
            float cosX =
                Mathf.Cos(
                    skewXRadians
                );

            float sinX =
                Mathf.Sin(
                    skewXRadians
                );

            float cosY =
                Mathf.Cos(
                    skewYRadians
                );

            float sinY =
                Mathf.Sin(
                    skewYRadians
                );

            float a =
                scaleX * cosY;

            float b =
                scaleX * sinY;

            float c =
                -scaleY * sinX;

            float d =
                scaleY * cosX;

            return new PvZReanimMatrix(
                a,
                c,
                x,

                b,
                d,
                y,

                0f,
                0f,
                1f
            );
        }

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

        public Vector3 GetPosition()
        {
            return new Vector3(
                m02,
                m12,
                0f
            );
        }

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