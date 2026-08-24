using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimAttachment : MonoBehaviour
    {
        private PvZReanimation source;
        private PvZReanimation target;

        private string sourceTrackName;

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
        }

        public void SetSource(
            PvZReanimation reanimation,
            string trackName)
        {
            source = reanimation;
            sourceTrackName = trackName;
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
        }
    }
}