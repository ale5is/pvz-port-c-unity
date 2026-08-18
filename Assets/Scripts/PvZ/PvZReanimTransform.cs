using UnityEngine;

namespace PvZReanim
{
    [System.Serializable]
    public class PvZReanimTransform
    {
        public float x;
        public float y;

        public float skewX;
        public float skewY;

        public float scaleX = 1f;
        public float scaleY = 1f;

        public float frame;

        public float alpha = 1f;

        public Sprite image;

        public string text = "";

        public bool HasX =>
            x != PvZReanimConstants.MissingValue;

        public bool HasY =>
            y != PvZReanimConstants.MissingValue;

        public bool HasSkewX =>
            skewX != PvZReanimConstants.MissingValue;

        public bool HasSkewY =>
            skewY != PvZReanimConstants.MissingValue;

        public bool HasScaleX =>
            scaleX != PvZReanimConstants.MissingValue;

        public bool HasScaleY =>
            scaleY != PvZReanimConstants.MissingValue;

        public bool HasFrame =>
            frame != PvZReanimConstants.MissingValue;

        public bool HasAlpha =>
            alpha != PvZReanimConstants.MissingValue;

        public PvZReanimTransform()
        {
            x = PvZReanimConstants.MissingValue;
            y = PvZReanimConstants.MissingValue;

            skewX = PvZReanimConstants.MissingValue;
            skewY = PvZReanimConstants.MissingValue;

            scaleX = PvZReanimConstants.MissingValue;
            scaleY = PvZReanimConstants.MissingValue;

            frame = PvZReanimConstants.MissingValue;
            alpha = PvZReanimConstants.MissingValue;

            image = null;
            text = "";
        }

        public PvZReanimTransform Clone()
        {
            return new PvZReanimTransform
            {
                x = x,
                y = y,
                skewX = skewX,
                skewY = skewY,
                scaleX = scaleX,
                scaleY = scaleY,
                frame = frame,
                alpha = alpha,
                image = image,
                text = text
            };
        }
    }
}