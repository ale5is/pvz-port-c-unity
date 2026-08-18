using System;
using UnityEngine;

namespace PvZReanim
{
    [Serializable]
    public class PvZReanimTransform
    {
        // =========================================================
        // IMAGE
        // =========================================================

        public string imageName;

        public Sprite image;

        // =========================================================
        // POSITION
        // =========================================================

        public float x =
            PvZReanimConstants.MissingValue;

        public float y =
            PvZReanimConstants.MissingValue;

        // =========================================================
        // SKEW
        // =========================================================

        public float skewX =
            PvZReanimConstants.MissingValue;

        public float skewY =
            PvZReanimConstants.MissingValue;

        // =========================================================
        // SCALE
        // =========================================================

        public float scaleX =
            PvZReanimConstants.MissingValue;

        public float scaleY =
            PvZReanimConstants.MissingValue;

        // =========================================================
        // FRAME
        // =========================================================

        public float frame =
            PvZReanimConstants.MissingValue;

        // =========================================================
        // ALPHA
        // =========================================================

        public float alpha =
            PvZReanimConstants.MissingValue;

        // =========================================================
        // TEXT
        // =========================================================

        public string text;

        // =========================================================
        // CLONE
        // =========================================================

        public PvZReanimTransform Clone()
        {
            PvZReanimTransform copy =
                new PvZReanimTransform();

            copy.imageName =
                imageName;

            copy.image =
                image;

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

        // =========================================================
        // DEFAULTS
        // =========================================================

        public void SetDefaults()
        {
            imageName =
                null;

            image =
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

        // =========================================================
        // IMAGE
        // =========================================================

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

        // =========================================================
        // VALIDATION
        // =========================================================

        public bool HasImageName =>
            !string.IsNullOrEmpty(
                imageName
            );

        public bool HasImage =>
            image != null;

        public bool HasPosition =>
            x !=
                PvZReanimConstants.MissingValue ||
            y !=
                PvZReanimConstants.MissingValue;

        public bool HasSkew =>
            skewX !=
                PvZReanimConstants.MissingValue ||
            skewY !=
                PvZReanimConstants.MissingValue;

        public bool HasScale =>
            scaleX !=
                PvZReanimConstants.MissingValue ||
            scaleY !=
                PvZReanimConstants.MissingValue;

        public bool HasFrame =>
            frame !=
            PvZReanimConstants.MissingValue;

        public bool HasAlpha =>
            alpha !=
            PvZReanimConstants.MissingValue;

        public bool HasText =>
            !string.IsNullOrEmpty(
                text
            );

        // =========================================================
        // POSITION
        // =========================================================

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

        // =========================================================
        // SKEW
        // =========================================================

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

        // =========================================================
        // SCALE
        // =========================================================

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

        // =========================================================
        // FRAME
        // =========================================================

        public float GetFrame(
            float fallback = 0f)
        {
            return frame ==
                PvZReanimConstants.MissingValue
                ? fallback
                : frame;
        }

        // =========================================================
        // ALPHA
        // =========================================================

        public float GetAlpha(
            float fallback = 1f)
        {
            return alpha ==
                PvZReanimConstants.MissingValue
                ? fallback
                : alpha;
        }

        // =========================================================
        // TEXT
        // =========================================================

        public string GetText(
            string fallback = null)
        {
            return string.IsNullOrEmpty(text)
                ? fallback
                : text;
        }
    }
}