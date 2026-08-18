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

            byte[] data =
                File.ReadAllBytes(path);

            return LoadBytes(data);
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

            string normalized =
                text
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

            string[] lines =
                normalized.Split('\n');

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
                        .Trim()
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
                                "PvZReanimParser: " +
                                "transform encontrado " +
                                "sin track en línea " +
                                (i + 1)
                            );

                            continue;
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
            string name =
                tokens.Length > 1
                    ? tokens[1]
                    : string.Empty;

            PvZReanimTrack track =
                new PvZReanimTrack(
                    Unquote(name)
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

            transform.x =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.y =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.skewX =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.skewY =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.scaleX =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.scaleY =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.frame =
                ReadFloat(
                    tokens,
                    ref index
                );

            transform.alpha =
                ReadFloat(
                    tokens,
                    ref index
                );

            /*
             * El nombre de imagen no se convierte
             * en Sprite aquí.
             *
             * El parser solamente conserva
             * el identificador original.
             *
             * Esto es importante porque el parser
             * no debe conocer cómo Unity almacena
             * las texturas.
             */

            if (index < tokens.Length)
            {
                string imageName =
                    Unquote(
                        tokens[index++]
                    );

                if (!IsMissingToken(imageName))
                {
                    transform.imageName =
                        imageName;
                }
            }

            /*
             * El texto también pertenece al frame.
             */

            if (index < tokens.Length)
            {
                string text =
                    Unquote(
                        tokens[index++]
                    );

                if (!IsMissingToken(text))
                {
                    transform.text =
                        text;
                }
            }
        }

        private static float ReadFloat(
            string[] tokens,
            ref int index)
        {
            if (index >= tokens.Length)
            {
                return PvZReanimConstants.MissingValue;
            }

            string value =
                tokens[index++];

            return ParseFloat(
                value
            );
        }

        private static void ParseGlobalProperty(
            PvZReanimDefinition definition,
            string[] tokens)
        {
            if (tokens.Length < 2)
                return;

            string property =
                tokens[0]
                    .Trim()
                    .ToLowerInvariant();

            string value =
                tokens[1];

            switch (property)
            {
                case "fps":

                    definition.fps =
                        ParseFloat(
                            value
                        );

                    break;

                case "framerate":

                    definition.fps =
                        ParseFloat(
                            value
                        );

                    break;

                case "rate":

                    definition.fps =
                        ParseFloat(
                            value
                        );

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

            value =
                Unquote(
                    value
                );

            if (IsMissingToken(value))
            {
                return PvZReanimConstants.MissingValue;
            }

            value =
                value.Replace(
                    ',',
                    '.'
                );

            float result;

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
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

        private static string Unquote(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value =
                value.Trim();

            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[value.Length - 1] == '"')
            {
                return value.Substring(
                    1,
                    value.Length - 2
                );
            }

            return value;
        }

        private static string RemoveComment(
            string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            bool quoted = false;

            for (int i = 0;
                 i < line.Length;
                 i++)
            {
                char c =
                    line[i];

                if (c == '"')
                {
                    quoted =
                        !quoted;

                    continue;
                }

                if (c == '#' &&
                    !quoted)
                {
                    return line.Substring(
                        0,
                        i
                    );
                }
            }

            return line;
        }

        private static string[] Tokenize(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return Array.Empty<string>();
            }

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
                    quoted =
                        !quoted;

                    current.Append(c);

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