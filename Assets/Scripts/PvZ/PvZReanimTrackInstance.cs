using UnityEngine;

namespace PvZReanim
{
    public class PvZReanimTrackInstance
    {
        public int blendCounter;

        public int blendTime;

        public PvZReanimTransform blendTransform;

        public float shakeOverride;

        public float shakeX;

        public float shakeY;

        public Sprite imageOverride;

        public PvZReanimRenderGroup renderGroup =
            PvZReanimRenderGroup.Normal;

        public Color trackColor =
            Color.white;

        public bool ignoreColorOverride;

        public bool truncateDisappearingFrames;

        public PvZReanimTrackInstance()
        {
            blendTransform =
                new PvZReanimTransform();

            blendCounter = 0;
            blendTime = 0;

            shakeOverride = 0f;
            shakeX = 0f;
            shakeY = 0f;

            imageOverride = null;

            trackColor = Color.white;
        }
    }
}