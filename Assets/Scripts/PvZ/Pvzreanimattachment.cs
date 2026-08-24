using UnityEngine;

namespace PvZReanim
{
    /*
     * Attachment equivalente a:
     *
     * Reanimation::AttachToAnotherReanimation
     *
     * + GetAttachmentOverlayMatrix
     *
     * del PvZ original.
     *
     * IMPORTANTE:
     *
     * Ya NO copiamos x/y del anim_stem directamente.
     *
     * El source calcula:
     *
     *     inverse(basePose) * currentPose
     *
     * y ese resultado se aplica al root del Reanimation
     * de la cabeza.
     */
    [DefaultExecutionOrder(100)]
    public class PvZReanimAttachment : MonoBehaviour
    {
        [Header("Fuente")]
        [SerializeField]
        private PvZReanimation source;

        [SerializeField]
        private string sourceTrackName =
            "anim_stem";

        private int cachedTrackIndex = -1;

        private string cachedTrackName;

        // =========================================================
        // SOURCE
        // =========================================================

        public void SetSource(
            PvZReanimation newSource,
            string newTrackName)
        {
            source =
                newSource;

            sourceTrackName =
                string.IsNullOrWhiteSpace(
                    newTrackName)
                    ? "anim_stem"
                    : newTrackName;

            cachedTrackIndex = -1;
            cachedTrackName = null;

            /*
             * Igual que el original:
             *
             * if (mFrameBasePose == -1)
             *     mFrameBasePose = mFrameStart;
             *
             * La pose base pertenece al BODY/source.
             */
            if (source != null &&
                source.FrameBasePose < 0)
            {
                source.SetFrameBasePose(
                    source.FrameStart
                );
            }
        }

        // =========================================================
        // UPDATE
        // =========================================================

        private void LateUpdate()
        {
            if (source == null)
                return;

            CacheTrack();

            if (cachedTrackIndex < 0)
                return;

            /*
             * Esta es la parte importante.
             *
             * NO:
             *
             *     x -> position.x
             *     y -> position.y
             *
             * Sino:
             *
             *     base^-1 * current
             */
            PvZReanimMatrix matrix =
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                );

            ApplyMatrix(
                matrix
            );
        }

        // =========================================================
        // CACHE
        // =========================================================

        private void CacheTrack()
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
                    "' en " +
                    source.name,
                    this
                );
            }
        }

        // =========================================================
        // APPLY MATRIX
        // =========================================================

        private void ApplyMatrix(
            PvZReanimMatrix matrix)
        {
            /*
             * Translation
             */
            transform.localPosition =
                new Vector3(
                    matrix.m02,
                    matrix.m12,
                    0f
                );

            /*
             * Extraemos los vectores de la matriz.
             *
             * Column 0:
             *
             *     m00
             *     m10
             *
             * Column 1:
             *
             *     m01
             *     m11
             */

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

            if (scaleX < 0.000001f)
                scaleX = 1f;

            if (scaleY < 0.000001f)
                scaleY = 1f;

            /*
             * Rotation.
             *
             * La matriz de Reanim usa:
             *
             *     m00 = cos(...)
             *     m10 = -sin(...)
             *
             * Por eso recuperamos el ángulo con atan2.
             */
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

            transform.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f
                );
        }

        // =========================================================
        // RESET
        // =========================================================

        public void ResetAttachment()
        {
            transform.localPosition =
                Vector3.zero;

            transform.localRotation =
                Quaternion.identity;

            transform.localScale =
                Vector3.one;
        }
    }
}