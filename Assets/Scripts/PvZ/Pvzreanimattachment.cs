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

        [Header("Destino")]
        [SerializeField]
        private PvZReanimation target;

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
        // UNITY
        // =========================================================

        private void Awake()
        {
            ResolveTarget();
        }

        private void Start()
        {
            ResolveTarget();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        // =========================================================
        // TARGET
        // =========================================================

        private void ResolveTarget()
        {
            if (target != null)
                return;

            /*
             * El RuntimeLoader vive en el mismo GameObject que
             * este Attachment y crea el PvZReanimation como hijo.
             * Por eso buscamos primero el loader.
             */
            PvZReanimRuntimeLoader loader =
                GetComponent<PvZReanimRuntimeLoader>();

            if (loader != null &&
                loader.Reanimation != null)
            {
                target = loader.Reanimation;
                return;
            }

            /*
             * Fallback por si el componente se usa manualmente
             * sobre un objeto que ya tiene una Reanimation.
             */
            target =
                GetComponentInChildren<PvZReanimation>();
        }

        public void SetTarget(
            PvZReanimation newTarget)
        {
            target = newTarget;

            if (target != null)
            {
                target.ResetOverlayMatrix();
            }

            Refresh();
        }

        public PvZReanimation GetTarget()
        {
            return target;
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

            ResolveTarget();
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
        // APPLY
        // =========================================================

        private void ApplyMatrix(
            PvZReanimMatrix matrix)
        {
            if (target == null)
                return;

            /*
             * IMPORTANTE:
             *
             * NO movemos transform.localPosition del objeto Head.
             *
             * El PvZ original NO hace eso.
             * AttachToAnotherReanimation() hace que la Reanimation
             * hija reciba esta matriz como mOverlayMatrix.
             *
             * Después el renderer de cada pieza hace:
             *
             *     transformDeLaPieza * overlayMatrix
             *
             * De esta forma TODAS las piezas de la cabeza reciben
             * el movimiento del anim_stem, incluso aunque sus
             * Transform de Unity sean distintos.
             */

            PvZReanimMatrix result = matrix;

            // -----------------------------------------------------
            // POSITION
            // -----------------------------------------------------

            if (!followPosition)
            {
                result.m02 = 0f;
                result.m12 = 0f;
            }

            // -----------------------------------------------------
            // ROTATION + SCALE
            // -----------------------------------------------------

            if (!followRotation && !followScale)
            {
                result.m00 = 1f;
                result.m01 = 0f;
                result.m10 = 0f;
                result.m11 = 1f;
            }
            else if (!followRotation)
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

                float determinant =
                    matrix.m00 * matrix.m11 -
                    matrix.m01 * matrix.m10;

                if (determinant < 0f)
                    scaleY = -scaleY;

                result.m00 = scaleX;
                result.m01 = 0f;
                result.m10 = 0f;
                result.m11 = scaleY;
            }
            else if (!followScale)
            {
                float angle =
                    Mathf.Atan2(
                        matrix.m10,
                        matrix.m00
                    );

                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                result.m00 = cos;
                result.m01 = -sin;
                result.m10 = sin;
                result.m11 = cos;
            }

            target.SetOverlayMatrix(result);
        }

        // =========================================================
        // REFRESH
        // =========================================================

        public void Refresh()
        {
            ResolveTarget();

            if (target == null)
                return;

            if (source == null)
            {
                target.ResetOverlayMatrix();
                return;
            }

            ResolveTrack();

            if (cachedTrackIndex < 0)
            {
                target.ResetOverlayMatrix();
                return;
            }

            PvZReanimMatrix matrix =
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                );

            ApplyMatrix(matrix);
        }

        // =========================================================
        // RESET
        // =========================================================

        public void ResetTransform()
        {
            if (target != null)
            {
                target.ResetOverlayMatrix();
            }
        }
    }
}
