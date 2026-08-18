using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PvZReanim
{
    public static class PvZReanimParser
    {
        private const string TrackToken = "track";
        private const string TransformToken = "transform";

        public static PvZReanimDefinition LoadFile(
            string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    "La ruta del archivo .reanim está vacía.",
                    nameof(path)
                );
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "No se encontró el archivo .reanim.",
                    path
                );
            }

            string text =
                File.ReadAllText(
                    path,
                    Encoding.UTF8
                );

            return Parse(text);
        }

        public static PvZReanimDefinition LoadBytes(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                throw new ArgumentException(
                    "Los datos del .reanim están vacíos.",
                    nameof(data)
                );
            }

            string text =
                Encoding.UTF8.GetString(data);

            return Parse(text);
        }

        public static PvZReanimDefinition Parse(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "El contenido del .reanim está vacío.",
                    nameof(text)
                );
            }

            string[] lines =
                text
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');

            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<
                    PvZReanimDefinition
                >();

            PvZReanimTrack currentTrack = null;

            for (int i = 0;
                 i < lines.Length;
                 i++)
            {
                string line =
                    RemoveComment(
                        lines[i]
                    ).Trim();

                if (string.IsNullOrEmpty(line))
                    continue;

                string[] tokens =
                    Tokenize(line);

                if (tokens.Length == 0)
                    continue;

                string command =
                    tokens[0]
                        .ToLowerInvariant();

                switch (command)
                {
                    case TrackToken:

                        currentTrack =
                            ParseTrack(
                                definition,
                                tokens
                            );

                        break;

                    case TransformToken:

                        if (currentTrack == null)
                        {
                            Debug.LogWarning(
                                $"PvZReanimParser: transform sin track en línea {i + 1}."
                            );

                            break;
                        }

                        ParseTransform(
                            currentTrack,
                            tokens
                        );

                        break;

                    default:

                        ParseGlobalProperty(
                            definition,
                            tokens
                        );

                        break;
                }
            }

            return definition;
        }

        private static PvZReanimTrack ParseTrack(
            PvZReanimDefinition definition,
            string[] tokens)
        {
            string trackName =
                tokens.Length > 1
                    ? tokens[1]
                    : string.Empty;

            PvZReanimTrack track =
                new PvZReanimTrack(
                    trackName
                );

            definition.tracks.Add(
                track
            );

            return track;
        }

        private static void ParseTransform(
            PvZReanimTrack track,
            string[] tokens)
        {
            PvZReanimTransform transform =
                new PvZReanimTransform();

            track.transforms.Add(
                transform
            );

            int index = 1;

            if (index < tokens.Length)
            {
                transform.x =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.y =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.skewX =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.skewY =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.scaleX =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.scaleY =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.frame =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                transform.alpha =
                    ParseFloat(
                        tokens[index++]
                    );
            }

            if (index < tokens.Length)
            {
                string image =
                    tokens[index++];

                if (!IsMissingToken(image))
                {
                    transform.imageName =
                        image;
                }
            }

            if (index < tokens.Length)
            {
                transform.text =
                    tokens[index++];
            }
        }

        private static void ParseGlobalProperty(
            PvZReanimDefinition definition,
            string[] tokens)
        {
            if (tokens.Length < 2)
                return;

            string property =
                tokens[0]
                    .ToLowerInvariant();

            string value =
                tokens[1];

            switch (property)
            {
                case "fps":

                    definition.fps =
                        ParseFloat(value);

                    break;

                case "framerate":

                    definition.fps =
                        ParseFloat(value);

                    break;

                case "rate":

                    definition.fps =
                        ParseFloat(value);

                    break;
            }
        }

        private static float ParseFloat(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return PvZReanimConstants.MissingValue;
            }

            if (IsMissingToken(value))
            {
                return PvZReanimConstants.MissingValue;
            }

            value =
                value.Replace(
                    ',',
                    '.'
                );

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float result))
            {
                return result;
            }

            return PvZReanimConstants.MissingValue;
        }

        private static bool IsMissingToken(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            string normalized =
                value.Trim()
                    .ToLowerInvariant();

            return
                normalized == "null" ||
                normalized == "none" ||
                normalized == "-" ||
                normalized == "missing" ||
                normalized == "undefined" ||
                normalized == "-10000";
        }

        private static string RemoveComment(
            string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            int commentIndex =
                line.IndexOf('#');

            if (commentIndex >= 0)
            {
                line =
                    line.Substring(
                        0,
                        commentIndex
                    );
            }

            return line;
        }

        private static string[] Tokenize(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Array.Empty<string>();

            List<string> tokens =
                new List<string>();

            StringBuilder current =
                new StringBuilder();

            bool quoted = false;

            for (int i = 0;
                 i < line.Length;
                 i++)
            {
                char c =
                    line[i];

                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (char.IsWhiteSpace(c) &&
                    !quoted)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(
                            current.ToString()
                        );

                        current.Clear();
                    }

                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                tokens.Add(
                    current.ToString()
                );
            }

            return tokens.ToArray();
        }
    }
}