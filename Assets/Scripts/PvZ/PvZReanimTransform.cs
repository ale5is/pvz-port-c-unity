using System;
using UnityEngine;

namespace PvZReanim
{
    [Serializable]
    public class PvZReanimTransform
    {
        public string imageName;

        public Sprite image;

        public string fontName;

        public float x =  PvZReanimConstants.MissingValue;

        public float y = PvZReanimConstants.MissingValue;

        public float skewX = PvZReanimConstants.MissingValue;

        public float skewY = PvZReanimConstants.MissingValue;

        public float scaleX = PvZReanimConstants.MissingValue;

        public float scaleY = PvZReanimConstants.MissingValue;

        public float frame = PvZReanimConstants.MissingValue;

        public float alpha = PvZReanimConstants.MissingValue;

        public string text;

        public PvZReanimTransform Clone()
        {
            PvZReanimTransform copy =
                new PvZReanimTransform();

            copy.imageName =
                imageName;

            copy.image =
                image;

            copy.fontName =
                fontName;

            copy.x =
                x;

            copy.y =
                y;

            copy.skewX =
                skewX;

            copy.skewY =
                skewY;

            copy.scaleX =
                scaleX;

            copy.scaleY =
                scaleY;

            copy.frame =
                frame;

            copy.alpha =
                alpha;

            copy.text =
                text;

            return copy;
        }

        public void SetDefaults()
        {
            imageName =
                null;

            image =
                null;

            fontName =
                null;

            x =
                0f;

            y =
                0f;

            skewX =
                0f;

            skewY =
                0f;

            scaleX =
                1f;

            scaleY =
                1f;

            frame =
                0f;

            alpha =
                1f;

            text =
                null;
        }

        public void SetImage(
            string name)
        {
            imageName =
                name;

            image =
                null;
        }

        public void SetSprite(
            Sprite sprite)
        {
            image =
                sprite;
        }

        public bool HasImageName =>
            !string.IsNullOrEmpty(
                imageName
            );

        public bool HasImage =>
            image != null;

        public bool HasFont =>
            !string.IsNullOrEmpty(
                fontName
            );

        public string GetFont(
            string fallback = null)
        {
            return string.IsNullOrEmpty(
                fontName
            )
                ? fallback
                : fontName;
        }

        public bool HasPosition =>
            x !=
                PvZReanimConstants.MissingValue ||
            y !=
                PvZReanimConstants.MissingValue;

        public float GetX(
            float fallback = 0f)
        {
            return x ==
                PvZReanimConstants.MissingValue
                ? fallback
                : x;
        }

        public float GetY(
            float fallback = 0f)
        {
            return y ==
                PvZReanimConstants.MissingValue
                ? fallback
                : y;
        }

        public bool HasSkew =>
            skewX !=
                PvZReanimConstants.MissingValue ||
            skewY !=
                PvZReanimConstants.MissingValue;

        public float GetSkewX(
            float fallback = 0f)
        {
            return skewX ==
                PvZReanimConstants.MissingValue
                ? fallback
                : skewX;
        }

        public float GetSkewY(
            float fallback = 0f)
        {
            return skewY ==
                PvZReanimConstants.MissingValue
                ? fallback
                : skewY;
        }

        public bool HasScale =>
            scaleX !=
                PvZReanimConstants.MissingValue ||
            scaleY !=
                PvZReanimConstants.MissingValue;

        public float GetScaleX(
            float fallback = 1f)
        {
            return scaleX ==
                PvZReanimConstants.MissingValue
                ? fallback
                : scaleX;
        }

        public float GetScaleY(
            float fallback = 1f)
        {
            return scaleY ==
                PvZReanimConstants.MissingValue
                ? fallback
                : scaleY;
        }

        public bool HasFrame =>
            frame !=
            PvZReanimConstants.MissingValue;

        public float GetFrame(
            float fallback = 0f)
        {
            return frame ==
                PvZReanimConstants.MissingValue
                ? fallback
                : frame;
        }

        public bool HasAlpha =>
            alpha !=
            PvZReanimConstants.MissingValue;

        public float GetAlpha(
            float fallback = 1f)
        {
            return alpha ==
                PvZReanimConstants.MissingValue
                ? fallback
                : alpha;
        }

        public bool HasText =>
            !string.IsNullOrEmpty(
                text
            );

        public string GetText(
            string fallback = null)
        {
            return string.IsNullOrEmpty(
                text
            )
                ? fallback
                : text;
        }
    }
}
