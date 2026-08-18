namespace PvZReanim
{
    public struct PvZReanimFrameTime
    {
        public float fraction;

        public int frameBefore;

        public int frameAfter;

        public PvZReanimFrameTime(
            float fraction,
            int frameBefore,
            int frameAfter)
        {
            this.fraction = fraction;
            this.frameBefore = frameBefore;
            this.frameAfter = frameAfter;
        }
    }
}