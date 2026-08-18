using UnityEngine;

namespace PvZReanim
{
    public enum PvZReanimLoopType
    {
        Once,
        Loop,
        PingPong
    }

    public enum PvZReanimRenderGroup
    {
        Hidden = -1,
        Normal = 0
    }

    public static class PvZReanimConstants
    {
        public const float DefaultFPS = 12f;

        // El recompilado utiliza este valor para campos inexistentes.
        public const float MissingValue = -10000f;

        public const int NoBasePose = -2;
    }
}