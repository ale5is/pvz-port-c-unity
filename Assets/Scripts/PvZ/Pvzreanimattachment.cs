using UnityEngine;

namespace PvZReanim
{
    /*
     * =============================================================
     * PVZ REANIM ATTACHMENT
     * =============================================================
     *
     * La cabeza NO debe recibir directamente:
     *
     *     current.x
     *     current.y
     *
     * del anim_stem.
     *
     * El Body calcula primero:
     *
     *     inverse(basePose) * currentPose
     *
     * mediante GetAttachmentOverlayMatrix().
     *
     * Este componente solamente aplica esa matriz relativa
     * al ROOT de la Reanimation de la cabeza.
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

        [Header("Seguimiento")]
        [SerializeField]
        private bool followPosition = true;

        [SerializeField]
        private bool followRotation = true;

        [SerializeField]
        private bool followScale = true;

        [Header("Corrección")]
        [SerializeField]
        private bool useOverlayMatrix = true;

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
                string.IsNullOrEmpty(
                    newTrackName
                )
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
             * =====================================================
             * OBTENER MATRIZ RELATIVA
             * =====================================================
             *
             * NO usamos:
             *
             *     transform.localPosition =
             *         current.GetX/Y();
             *
             * porque eso toma la posición absoluta del anim_stem.
             *
             * El Reanimation ya calcula la transformación relativa
             * correcta mediante GetAttachmentOverlayMatrix().
             */
            PvZReanimMatrix matrix;

            if (useOverlayMatrix)
            {
                matrix =
                    source.GetAttachmentOverlayMatrix(
                        cachedTrackIndex
                    );
            }
            else
            {
                matrix =
                    PvZReanimMatrix.Identity;
            }

            ApplyMatrix(
                matrix
            );
        }

        // =========================================================
        // FIND TRACK
        // =========================================================

        private void ResolveTrack()
        {
            if (cachedTrackIndex >= 0 &&
                cachedTrackName ==
                sourceTrackName)
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
        // APPLY MATRIX
        // =========================================================

        private void ApplyMatrix(
            PvZReanimMatrix matrix)
        {
            /*
             * =====================================================
             * POSITION
             * =====================================================
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

            /*
             * =====================================================
             * SCALE
             * =====================================================
             *
             * La matriz puede contener skew.
             *
             * Unity Transform no soporta shear directamente,
             * por lo que extraemos la escala de los ejes.
             */

            float scaleX =
                Mathf.Sqrt(
                    matrix.m00 *
                    matrix.m00 +
                    matrix.m10 *
                    matrix.m10
                );

            float scaleY =
                Mathf.Sqrt(
                    matrix.m01 *
                    matrix.m01 +
                    matrix.m11 *
                    matrix.m11
                );

            if (scaleX < 0.000001f ||
                float.IsNaN(scaleX) ||
                float.IsInfinity(scaleX))
            {
                scaleX = 1f;
            }

            if (scaleY < 0.000001f ||
                float.IsNaN(scaleY) ||
                float.IsInfinity(scaleY))
            {
                scaleY = 1f;
            }

            /*
             * =====================================================
             * ROTATION
             * =====================================================
             *
             * El eje X de la matriz contiene la orientación.
             *
             * No usamos directamente skewY.
             */

            if (followRotation)
            {
                float rotation =
                    Mathf.Atan2(
                        matrix.m10,
                        matrix.m00
                    ) *
                    Mathf.Rad2Deg;

                transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        rotation
                    );
            }

            /*
             * =====================================================
             * SCALE
             * =====================================================
             */

            if (followScale)
            {
                /*
                 * Detectar reflexión.
                 *
                 * Determinante negativo significa que uno de los
                 * ejes está invertido.
                 */

                float determinant =
                    matrix.m00 *
                    matrix.m11 -
                    matrix.m01 *
                    matrix.m10;

                if (determinant < 0f)
                {
                    scaleY =
                        -scaleY;
                }

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
        // PUBLIC
        // =========================================================

        public void Refresh()
        {
            cachedTrackIndex = -1;

            cachedTrackName = null;

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

            PvZReanimMatrix matrix =
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                );

            ApplyMatrix(
                matrix
            );
        }
    }
}