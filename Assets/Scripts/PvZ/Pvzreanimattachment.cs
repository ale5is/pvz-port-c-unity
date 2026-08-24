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
        // POSE BASE DEL OBJETO
        // =========================================================

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 baseLocalScale;

        private bool basePoseCaptured;

        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            CaptureBasePose();
        }

        private void Start()
        {
            CaptureBasePose();
            Refresh();
        }

        private void LateUpdate()
        {
            if (source == null)
                return;

            if (!basePoseCaptured)
                CaptureBasePose();

            ResolveTrack();

            if (cachedTrackIndex < 0)
                return;

            PvZReanimMatrix matrix =
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                );

            ApplyMatrix(matrix);
        }

        // =========================================================
        // BASE POSE
        // =========================================================

        public void CaptureBasePose()
        {
            baseLocalPosition =
                transform.localPosition;

            baseLocalRotation =
                transform.localRotation;

            baseLocalScale =
                transform.localScale;

            basePoseCaptured = true;
        }

        public void ResetToBasePose()
        {
            if (!basePoseCaptured)
                CaptureBasePose();

            transform.localPosition =
                baseLocalPosition;

            transform.localRotation =
                baseLocalRotation;

            transform.localScale =
                baseLocalScale;
        }

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

            /*
             * IMPORTANTE:
             *
             * La posición actual del Head es su pose base.
             * No debemos ponerla en Vector3.zero.
             */
            CaptureBasePose();

            Refresh();
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
        // TRACK
        // =========================================================

        private void ResolveTrack()
        {
            if (source == null)
                return;

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
        // APPLY MATRIX
        // =========================================================

        private void ApplyMatrix(
            PvZReanimMatrix matrix)
        {
            /*
             * matrix NO es una posición absoluta.
             *
             * Es:
             *
             *      basePose^-1 * currentPose
             *
             * Por lo tanto:
             *
             * m02/m12 = desplazamiento relativo
             *
             * Ese desplazamiento se aplica sobre la
             * posición original del Head.
             */

            Vector3 relativePosition =
                new Vector3(
                    matrix.m02,
                    matrix.m12,
                    0f
                );

            // =====================================================
            // POSITION
            // =====================================================

            if (followPosition)
            {
                /*
                 * El desplazamiento está en el espacio local
                 * del attachment.
                 *
                 * Lo convertimos usando la rotación/escala
                 * de la pose base.
                 */
                Vector3 scaledRelative =
                    Vector3.Scale(
                        relativePosition,
                        baseLocalScale
                    );

                Vector3 rotatedRelative =
                    baseLocalRotation *
                    scaledRelative;

                transform.localPosition =
                    baseLocalPosition +
                    rotatedRelative;
            }
            else
            {
                transform.localPosition =
                    baseLocalPosition;
            }

            // =====================================================
            // ROTATION
            // =====================================================

            if (followRotation)
            {
                float angle =
                    Mathf.Atan2(
                        matrix.m10,
                        matrix.m00
                    ) *
                    Mathf.Rad2Deg;

                Quaternion relativeRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle
                    );

                transform.localRotation =
                    baseLocalRotation *
                    relativeRotation;
            }
            else
            {
                transform.localRotation =
                    baseLocalRotation;
            }

            // =====================================================
            // SCALE
            // =====================================================

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
                        baseLocalScale.x * scaleX,
                        baseLocalScale.y * scaleY,
                        baseLocalScale.z
                    );
            }
            else
            {
                transform.localScale =
                    baseLocalScale;
            }
        }

        // =========================================================
        // REFRESH
        // =========================================================

        public void Refresh()
        {
            if (source == null)
            {
                ResetToBasePose();
                return;
            }

            if (!basePoseCaptured)
                CaptureBasePose();

            ResolveTrack();

            if (cachedTrackIndex < 0)
            {
                ResetToBasePose();
                return;
            }

            ApplyMatrix(
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                )
            );
        }

        // =========================================================
        // RESET
        // =========================================================

        public void ResetTransform()
        {
            ResetToBasePose();
        }
    }
}