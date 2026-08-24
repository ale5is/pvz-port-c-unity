using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimAttachment : MonoBehaviour
    {
        private PvZReanimation source;
        private PvZReanimation target;

        private string sourceTrackName;

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