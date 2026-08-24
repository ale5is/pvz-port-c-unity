using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimAttachment : MonoBehaviour
    {
        private PvZReanimation source;
        private PvZReanimation target;

        private string sourceTrackName;

        /*
         * Guardamos el ultimo trackIndex resuelto para no
         * recalcular/reaplicar el sortingOrder del target en
         * cada frame si no cambio nada (SetSortingOrderBase
         * recorre todos los tracks del target).
         */
        private int lastSourceTrackIndex = -1;

        public PvZReanimation Source =>
            source;

        public PvZReanimation Target =>
            target;

        public string SourceTrackName =>
            sourceTrackName;

        public void SetTarget(
            PvZReanimation reanimation)
        {
            target = reanimation;
            lastSourceTrackIndex = -1;
        }

        public void SetSource(
            PvZReanimation reanimation,
            string trackName)
        {
            source = reanimation;
            sourceTrackName = trackName;
            lastSourceTrackIndex = -1;
        }

        public void Refresh()
        {
            if (source == null ||
                target == null ||
                string.IsNullOrEmpty(sourceTrackName))
            {
                return;
            }

            int trackIndex =
                source.GetTrackIndex(
                    sourceTrackName
                );

            if (trackIndex < 0)
                return;

            /*
             * EXACTAMENTE la lógica del Recompiled:
             *
             * GetCurrentTransform()
             * MatrixFromTransform()
             * current * mOverlayMatrix
             * inverse(basePose)
             * current * inverse(basePose)
             */
            PvZReanimMatrix overlay =
                source.GetAttachmentOverlayMatrix(
                    trackIndex
                );

            target.SetOverlayMatrix(
                overlay
            );

            RefreshSortingOrder(
                trackIndex
            );
        }

        // =========================================================
        // SORTING
        // =========================================================

        /*
         * El Reanim original dibuja el attachment (ej: la cabeza)
         * intercalado exactamente en el punto del loop donde
         * aparece el track anfitrión (DrawRenderGroup,
         * Reanimator.cpp linea 860-877), y no como un bloque
         * separado antes o despues de TODO el padre.
         *
         * Para reproducir eso con sortingOrder de Unity: le
         * pedimos al source el "hueco" de sortingOrder que le
         * corresponde al track anfitrión
         * (GetSortingOrderForTrack) y ahi adentro ubicamos TODOS
         * los tracks del target (+1, para quedar justo encima del
         * track anfitrión pero por debajo del siguiente).
         */
        private void RefreshSortingOrder(
            int trackIndex)
        {
            if (trackIndex == lastSourceTrackIndex)
                return;

            lastSourceTrackIndex = trackIndex;

            int hostOrder =
                source.GetSortingOrderForTrack(
                    trackIndex
                );

            target.SetSortingOrderBase(
                hostOrder + 1
            );
        }
    }
}