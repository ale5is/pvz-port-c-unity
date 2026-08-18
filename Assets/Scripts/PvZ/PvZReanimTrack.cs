using System.Collections.Generic;

namespace PvZReanim
{
    [System.Serializable]
    public class PvZReanimTrack
    {
        public string name;

        public List<PvZReanimTransform> transforms =
            new List<PvZReanimTransform>();

        public int TransformCount =>
            transforms != null ? transforms.Count : 0;

        public PvZReanimTrack()
        {
            name = "";
        }

        public PvZReanimTrack(string trackName)
        {
            name = trackName;
        }

        public PvZReanimTransform GetTransform(int index)
        {
            if (transforms == null ||
                transforms.Count == 0)
                return null;

            index =
                System.Math.Max(
                    0,
                    System.Math.Min(
                        index,
                        transforms.Count - 1
                    )
                );

            return transforms[index];
        }
    }
}