using System.Collections.Generic;
using UnityEngine;

namespace PvZReanim
{
    [CreateAssetMenu(
        fileName = "PvZReanimDefinition",
        menuName = "PvZ/Reanim Definition"
    )]
    public class PvZReanimDefinition : ScriptableObject
    {
        [Min(0.01f)]
        public float fps = PvZReanimConstants.DefaultFPS;

        public List<PvZReanimTrack> tracks =
            new List<PvZReanimTrack>();

        public int TrackCount =>
            tracks != null ? tracks.Count : 0;

        public PvZReanimTrack GetTrack(int index)
        {
            if (tracks == null ||
                index < 0 ||
                index >= tracks.Count)
                return null;

            return tracks[index];
        }

        public PvZReanimTrack GetTrack(string trackName)
        {
            if (tracks == null)
                return null;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].name == trackName)
                    return tracks[i];
            }

            return null;
        }

        public int FindTrackIndex(string trackName)
        {
            if (tracks == null)
                return -1;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].name == trackName)
                    return i;
            }

            return -1;
        }

        public int GetMaxFrameCount()
        {
            int result = 0;

            if (tracks == null)
                return result;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] == null)
                    continue;

                if (tracks[i].TransformCount > result)
                    result = tracks[i].TransformCount;
            }

            return result;
        }
    }
}