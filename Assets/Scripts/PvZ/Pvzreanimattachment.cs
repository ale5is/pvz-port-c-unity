using UnityEngine;

namespace PvZReanim
{
    [DefaultExecutionOrder(100)]
    public class PvZReanimAttachment : MonoBehaviour
    {
        [Header("Fuente")]
        [SerializeField]
        private PvZReanimation source;

        [SerializeField]
        private string sourceTrackName = "anim_stem";

        [Header("Seguimiento")]
        [SerializeField]
        private bool followPosition = true;

        [SerializeField]
        private bool followRotation = true;

        [SerializeField]
        private bool followScale = true;

        private int cachedTrackIndex = -1;
        private string cachedTrackName;

        // =========================================================
        // SOURCE
        // =========================================================

        public void SetSource(
            PvZReanimation newSource,
            string newTrackName)
        {
            source = newSource;

            sourceTrackName =
                string.IsNullOrEmpty(newTrackName)
                    ? "anim_stem"
                    : newTrackName;

            cachedTrackIndex = -1;
            cachedTrackName = null;

            ResetTransform();
        }

        public PvZReanimation GetSource()
        {
            return source;
        }

        public string GetSourceTrackName()
        {
            return sourceTrackName;
        }

        // =========================================================
        // UNITY
        // =========================================================

        private void LateUpdate()
        {
            if (source == null)
                return;

            ResolveTrack();

            if (cachedTrackIndex < 0)
                return;

            /*
             * IMPORTANTE:
             *
             * El PvZ original NO copia x/y directamente.
             *
             * AttachToAnotherReanimation() utiliza:
             *
             *     GetAttachmentOverlayMatrix()
             *
             * y esa matriz representa el movimiento RELATIVO
             * del track respecto de su pose base.
             */
            PvZReanimMatrix matrix =
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                );

            ApplyMatrix(matrix);
        }

        // =========================================================
        // TRACK
        // =========================================================

        private void ResolveTrack()
        {
            if (cachedTrackIndex >= 0 &&
                cachedTrackName == sourceTrackName)
            {
                return;
            }

            cachedTrackIndex =
                source.FindTrackIndex(
                    sourceTrackName
                );

            cachedTrackName =
                sourceTrackName;

            if (cachedTrackIndex < 0)
            {
                Debug.LogWarning(
                    "[PvZReanimAttachment] " +
                    "No se encontró el track '" +
                    sourceTrackName +
                    "' en '" +
                    source.name +
                    "'.",
                    this
                );
            }
        }

        // =========================================================
        // APPLY
        // =========================================================

        private void ApplyMatrix(
            PvZReanimMatrix matrix)
        {
            /*
             * El attachment original contiene una matriz completa.
             *
             * Unity Transform no puede representar shear/skew
             * completamente, pero posición + rotación + escala
             * permiten reproducir correctamente la parte principal.
             */

            if (followPosition)
            {
                transform.localPosition =
                    new Vector3(
                        matrix.m02,
                        matrix.m12,
                        0f
                    );
            }

            if (followRotation)
            {
                float angle =
                    Mathf.Atan2(
                        matrix.m10,
                        matrix.m00
                    ) *
                    Mathf.Rad2Deg;

                transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle
                    );
            }

            if (followScale)
            {
                float scaleX =
                    Mathf.Sqrt(
                        matrix.m00 * matrix.m00 +
                        matrix.m10 * matrix.m10
                    );

                float scaleY =
                    Mathf.Sqrt(
                        matrix.m01 * matrix.m01 +
                        matrix.m11 * matrix.m11
                    );

                if (float.IsNaN(scaleX) ||
                    float.IsInfinity(scaleX) ||
                    scaleX < 0.000001f)
                {
                    scaleX = 1f;
                }

                if (float.IsNaN(scaleY) ||
                    float.IsInfinity(scaleY) ||
                    scaleY < 0.000001f)
                {
                    scaleY = 1f;
                }

                float determinant =
                    matrix.m00 * matrix.m11 -
                    matrix.m01 * matrix.m10;

                if (determinant < 0f)
                    scaleY = -scaleY;

                transform.localScale =
                    new Vector3(
                        scaleX,
                        scaleY,
                        1f
                    );
            }
        }

        // =========================================================
        // RESET
        // =========================================================

        private void ResetTransform()
        {
            transform.localPosition =
                Vector3.zero;

            transform.localRotation =
                Quaternion.identity;

            transform.localScale =
                Vector3.one;
        }

        // =========================================================
        // REFRESH
        // =========================================================

        public void Refresh()
        {
            if (source == null)
            {
                ResetTransform();
                return;
            }

            ResolveTrack();

            if (cachedTrackIndex < 0)
            {
                ResetTransform();
                return;
            }

            ApplyMatrix(
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                )
            );
        }
    }
}