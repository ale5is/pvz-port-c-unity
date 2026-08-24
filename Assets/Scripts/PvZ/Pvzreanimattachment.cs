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

        /*
         * IMPORTANTE
         *
         * El Reanimation original de PvZ no convierte la matriz
         * del attachment directamente a localPosition/localRotation.
         *
         * La matriz calculada por GetAttachmentOverlayMatrix()
         * se utiliza como mOverlayMatrix del Reanimation adjunto.
         *
         * En Unity no podemos asignar una matriz affine completa
         * a Transform, pero podemos reconstruirla correctamente
         * mediante posición + rotación + escala.
         */

        private int cachedTrackIndex = -1;
        private string cachedTrackName;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 baseLocalScale;

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

            SaveBaseTransform();
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
        // UNITY
        // =========================================================

        private void Awake()
        {
            SaveBaseTransform();
        }

        private void Start()
        {
            ResolveTrack();
            Refresh();
        }

        private void LateUpdate()
        {
            if (source == null)
                return;

            ResolveTrack();

            if (cachedTrackIndex < 0)
                return;

            ApplyAttachmentMatrix(
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                )
            );
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

        private void ApplyAttachmentMatrix(
            PvZReanimMatrix matrix)
        {
            /*
             * La matriz PvZ tiene esta forma:
             *
             * | m00 m01 m02 |
             * | m10 m11 m12 |
             * |  0   0   1  |
             *
             * m02/m12 = posición
             *
             * La parte 2x2 contiene:
             *
             * escala + rotación + skew.
             *
             * No usamos simplemente:
             *
             * atan2(m10,m00)
             *
             * porque eso da resultados incorrectos cuando existe
             * skew, que es habitual en los .reanim de PvZ.
             */

            float a = matrix.m00;
            float b = matrix.m01;
            float c = matrix.m10;
            float d = matrix.m11;

            // -----------------------------------------------------
            // POSITION
            // -----------------------------------------------------

            if (followPosition)
            {
                transform.localPosition =
                    new Vector3(
                        matrix.m02,
                        matrix.m12,
                        baseLocalPosition.z
                    );
            }

            // -----------------------------------------------------
            // DECOMPOSICIÓN 2D
            // -----------------------------------------------------

            float scaleX =
                Mathf.Sqrt(
                    a * a +
                    c * c
                );

            if (scaleX < 0.000001f ||
                float.IsNaN(scaleX) ||
                float.IsInfinity(scaleX))
            {
                scaleX = 1f;
            }

            /*
             * Normalizamos la primera columna.
             */
            float r00 = a / scaleX;
            float r10 = c / scaleX;

            /*
             * Proyección de la segunda columna sobre la primera.
             *
             * Esto elimina la parte de shear antes de obtener
             * la rotación.
             */
            float shear =
                r00 * b +
                r10 * d;

            float orthogonalX =
                b -
                r00 * shear;

            float orthogonalY =
                d -
                r10 * shear;

            float scaleY =
                Mathf.Sqrt(
                    orthogonalX * orthogonalX +
                    orthogonalY * orthogonalY
                );

            if (scaleY < 0.000001f ||
                float.IsNaN(scaleY) ||
                float.IsInfinity(scaleY))
            {
                scaleY = 1f;
            }

            /*
             * Detectamos inversión.
             */
            float determinant =
                a * d -
                b * c;

            if (determinant < 0f)
            {
                scaleY = -scaleY;
            }

            // -----------------------------------------------------
            // ROTACIÓN
            // -----------------------------------------------------

            if (followRotation)
            {
                float angle =
                    Mathf.Atan2(
                        r10,
                        r00
                    ) *
                    Mathf.Rad2Deg;

                transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle
                    );
            }

            // -----------------------------------------------------
            // SCALE
            // -----------------------------------------------------

            if (followScale)
            {
                transform.localScale =
                    new Vector3(
                        scaleX,
                        scaleY,
                        baseLocalScale.z
                    );
            }
        }

        // =========================================================
        // RESET / BASE
        // =========================================================

        private void SaveBaseTransform()
        {
            baseLocalPosition =
                transform.localPosition;

            baseLocalRotation =
                transform.localRotation;

            baseLocalScale =
                transform.localScale;
        }

        private void ResetTransform()
        {
            transform.localPosition =
                baseLocalPosition;

            transform.localRotation =
                baseLocalRotation;

            transform.localScale =
                baseLocalScale;
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

            ApplyAttachmentMatrix(
                source.GetAttachmentOverlayMatrix(
                    cachedTrackIndex
                )
            );
        }

        // =========================================================
        // FORCE TRACK
        // =========================================================

        public void SetTrack(
            string trackName)
        {
            sourceTrackName =
                string.IsNullOrEmpty(trackName)
                    ? "anim_stem"
                    : trackName;

            cachedTrackIndex = -1;
            cachedTrackName = null;

            ResolveTrack();
            Refresh();
        }

        // =========================================================
        // ENABLE / DISABLE FOLLOW
        // =========================================================

        public void SetFollowPosition(
            bool value)
        {
            followPosition = value;
        }

        public void SetFollowRotation(
            bool value)
        {
            followRotation = value;
        }

        public void SetFollowScale(
            bool value)
        {
            followScale = value;
        }

        // =========================================================
        // SOURCE REFRESH
        // =========================================================

        public void ClearSource()
        {
            source = null;

            cachedTrackIndex = -1;
            cachedTrackName = null;

            ResetTransform();
        }
    }
}