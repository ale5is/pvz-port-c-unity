using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimLoader
    {
        public static PvZReanimDefinition
            CreateDefinition(
                string name,
                float fps)
        {
            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<
                    PvZReanimDefinition
                >();

            definition.name = name;

            definition.fps =
                fps > 0f
                    ? fps
                    : PvZReanimConstants.DefaultFPS;

            return definition;
        }

        public static PvZReanimTrack
            AddTrack(
                PvZReanimDefinition definition,
                string name)
        {
            PvZReanimTrack track =
                new PvZReanimTrack(name);

            definition.tracks.Add(track);

            return track;
        }

        public static PvZReanimTransform
            AddFrame(
                PvZReanimTrack track)
        {
            PvZReanimTransform transform =
                new PvZReanimTransform();

            track.transforms.Add(
                transform
            );

            return transform;
        }
    }
}