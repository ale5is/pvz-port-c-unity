namespace PvZReanim
{
    public static class PvZReanimDataFiller
    {
        public static void FillDefinition(
            PvZReanimDefinition definition)
        {
            if (definition == null ||
                definition.tracks == null)
            {
                return;
            }

            for (int i = 0;
                 i < definition.tracks.Count;
                 i++)
            {
                FillTrack(
                    definition.tracks[i]
                );
            }
        }

        public static void FillTrack(
            PvZReanimTrack track)
        {
            if (track == null ||
                track.transforms == null)
            {
                return;
            }

            float previousX = 0f;
            float previousY = 0f;

            float previousSkewX = 0f;
            float previousSkewY = 0f;

            float previousScaleX = 1f;
            float previousScaleY = 1f;

            float previousFrame = 0f;
            float previousAlpha = 1f;

            string previousImage = null;
            string previousFont = null;
            string previousText = "";

            for (int i = 0;
                 i < track.transforms.Count;
                 i++)
            {
                PvZReanimTransform transform =
                    track.transforms[i];

                if (transform == null)
                {
                    transform =
                        new PvZReanimTransform();

                    track.transforms[i] =
                        transform;
                }

                FillFloat(
                    ref previousX,
                    ref transform.x
                );

                FillFloat(
                    ref previousY,
                    ref transform.y
                );

                FillFloat(
                    ref previousSkewX,
                    ref transform.skewX
                );

                FillFloat(
                    ref previousSkewY,
                    ref transform.skewY
                );

                FillFloat(
                    ref previousScaleX,
                    ref transform.scaleX
                );

                FillFloat(
                    ref previousScaleY,
                    ref transform.scaleY
                );

                FillFloat(
                    ref previousFrame,
                    ref transform.frame
                );

                FillFloat(
                    ref previousAlpha,
                    ref transform.alpha
                );

                if (string.IsNullOrEmpty(
                        transform.imageName
                    ))
                {
                    transform.imageName =
                        previousImage;
                }
                else
                {
                    previousImage =
                        transform.imageName;
                }

                if (string.IsNullOrEmpty(
                        transform.fontName
                    ))
                {
                    transform.fontName =
                        previousFont;
                }
                else
                {
                    previousFont =
                        transform.fontName;
                }

                if (string.IsNullOrEmpty(
                        transform.text
                    ))
                {
                    transform.text =
                        previousText;
                }
                else
                {
                    previousText =
                        transform.text;
                }
            }
        }

        private static void FillFloat(
            ref float previous,
            ref float value)
        {
            if (IsMissingValue(value))
            {
                value = previous;
            }
            else
            {
                previous = value;
            }
        }

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                   PvZReanimConstants.MissingValue;
        }
    }
}