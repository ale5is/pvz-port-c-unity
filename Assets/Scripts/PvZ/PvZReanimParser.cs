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
        // =========================================================
        // CARGAR ARCHIVO
        // =========================================================

        public static PvZReanimDefinition LoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
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

            return LoadBytes(File.ReadAllBytes(path));
        }

        // =========================================================
        // CARGAR BYTES
        // =========================================================

        public static PvZReanimDefinition LoadBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException(
                    "Los datos del .reanim están vacíos.",
                    nameof(data)
                );
            }

            string text = DecodeText(data);

            return Parse(text);
        }

        // =========================================================
        // DECODIFICAR
        // =========================================================

        private static string DecodeText(byte[] data)
        {
            // UTF-8 BOM
            if (data.Length >= 3 &&
                data[0] == 0xEF &&
                data[1] == 0xBB &&
                data[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(
                    data,
                    3,
                    data.Length - 3
                );
            }

            // UTF-16 LE BOM
            if (data.Length >= 2 &&
                data[0] == 0xFF &&
                data[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(
                    data,
                    2,
                    data.Length - 2
                );
            }

            // UTF-16 BE BOM
            if (data.Length >= 2 &&
                data[0] == 0xFE &&
                data[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(
                    data,
                    2,
                    data.Length - 2
                );
            }

            return Encoding.UTF8.GetString(data);
        }

        // =========================================================
        // PARSER PRINCIPAL
        // =========================================================

        public static PvZReanimDefinition Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "El contenido del .reanim está vacío.",
                    nameof(text)
                );
            }

            PvZReanimDefinition definition =
                ParsePvZReanimText(text);

            if (definition != null)
            {
                int frameCount =
                    definition.GetMaxFrameCount();

                Debug.Log(
                    "[PvZReanimParser] Reanim parseado correctamente | " +
                    "FPS: " + definition.fps +
                    " | Tracks: " + definition.TrackCount +
                    " | Frames: " + frameCount
                );
            }

            return definition;
        }

        // =========================================================
        // PARSER PVZ REANIM
        // =========================================================

        private static PvZReanimDefinition ParsePvZReanimText(
            string text)
        {
            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<PvZReanimDefinition>();

            definition.fps =
                PvZReanimConstants.DefaultFPS;

            // Normalizar saltos
            text = text.Replace("\r\n", "\n");
            text = text.Replace('\r', '\n');

            // =====================================================
            // FPS
            // =====================================================

            float fps =
                FindFirstFloat(
                    text,
                    "<fps>",
                    "</fps>"
                );

            if (!IsMissingValue(fps) &&
                fps > 0f)
            {
                definition.fps = fps;
            }

            // =====================================================
            // TRACKS
            // =====================================================

            int searchPosition = 0;

            while (true)
            {
                int trackStart =
                    FindOpeningTag(
                        text,
                        "track",
                        searchPosition
                    );

                if (trackStart < 0)
                    break;

                int trackEnd =
                    FindClosingTag(
                        text,
                        "track",
                        trackStart
                    );

                if (trackEnd < 0)
                {
                    Debug.LogWarning(
                        "[PvZReanimParser] Track sin cierre."
                    );

                    break;
                }

                string trackText =
                    text.Substring(
                        trackStart,
                        trackEnd - trackStart
                    );

                PvZReanimTrack track =
                    ParseTrackText(
                        trackText,
                        definition.TrackCount
                    );

                if (track != null)
                {
                    definition.tracks.Add(track);
                }

                searchPosition =
                    trackEnd +
                    "</track>".Length;
            }

            // =====================================================
            // FALLBACK
            // =====================================================

            if (definition.TrackCount == 0)
            {
                ParseIndependentXmlBlocks(
                    text,
                    definition
                );
            }

            return definition;
        }

        // =========================================================
        // PARSE TRACK
        // =========================================================

        private static PvZReanimTrack ParseTrackText(
            string text,
            int index)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // =====================================================
            // NOMBRE
            // =====================================================

            string trackName =
                FindFirstString(
                    text,
                    "<name>",
                    "</name>"
                );

            if (string.IsNullOrWhiteSpace(trackName))
            {
                trackName =
                    FindAttribute(
                        text,
                        "name"
                    );
            }

            if (string.IsNullOrWhiteSpace(trackName))
            {
                trackName =
                    "track_" + index;
            }

            trackName =
                CleanValue(trackName);

            PvZReanimTrack track =
                new PvZReanimTrack(trackName);

            // =====================================================
            // TRANSFORMS
            //
            // IMPORTANTE:
            //
            // Resodded define:
            //
            // track -> t[]
            //
            // Cada <t> es un elemento COMPLETO.
            //
            // =====================================================

            int position = 0;

            while (true)
            {
                int start =
                    FindOpeningTag(
                        text,
                        "t",
                        position
                    );

                if (start < 0)
                    break;

                int end =
                    FindClosingTag(
                        text,
                        "t",
                        start
                    );

                if (end < 0)
                {
                    Debug.LogWarning(
                        "[PvZReanimParser] Transform <t> sin cierre " +
                        "en track: " + track.name
                    );

                    break;
                }

                string element =
                    text.Substring(
                        start,
                        end - start +
                        "</t>".Length
                    );

                PvZReanimTransform transform =
                    ParseTransformElement(
                        element
                    );

                if (transform != null)
                {
                    track.transforms.Add(transform);
                }

                position =
                    end +
                    "</t>".Length;
            }

            // =====================================================
            // COMPATIBILIDAD CON <transform>
            // =====================================================

            if (track.TransformCount == 0)
            {
                position = 0;

                while (true)
                {
                    int start =
                        FindOpeningTag(
                            text,
                            "transform",
                            position
                        );

                    if (start < 0)
                        break;

                    int end =
                        FindClosingTag(
                            text,
                            "transform",
                            start
                        );

                    if (end < 0)
                        break;

                    string element =
                        text.Substring(
                            start,
                            end - start +
                            "</transform>".Length
                        );

                    PvZReanimTransform transform =
                        ParseTransformElement(
                            element
                        );

                    if (transform != null)
                    {
                        track.transforms.Add(
                            transform
                        );
                    }

                    position =
                        end +
                        "</transform>".Length;
                }
            }

            Debug.Log(
                "[PvZReanimParser] Track: " +
                track.name +
                " | Frames: " +
                track.TransformCount
            );

            return track;
        }

        // =========================================================
        // PARSE TRANSFORM
        // =========================================================

        private static PvZReanimTransform ParseTransformElement(
            string element)
        {
            if (string.IsNullOrWhiteSpace(element))
                return null;

            PvZReanimTransform transform =
                new PvZReanimTransform();

            // =====================================================
            // CAMPOS OFICIALES DE RESODDED
            //
            // x
            // y
            // kx
            // ky
            // sx
            // sy
            // f
            // a
            // i
            // font
            // text
            // =====================================================

            transform.x =
                ReadValue(
                    element,
                    "x"
                );

            transform.y =
                ReadValue(
                    element,
                    "y"
                );

            transform.skewX =
                ReadValue(
                    element,
                    "kx"
                );

            transform.skewY =
                ReadValue(
                    element,
                    "ky"
                );

            transform.scaleX =
                ReadValue(
                    element,
                    "sx"
                );

            transform.scaleY =
                ReadValue(
                    element,
                    "sy"
                );

            transform.frame =
                ReadValue(
                    element,
                    "f"
                );

            transform.alpha =
                ReadValue(
                    element,
                    "a"
                );

            // =====================================================
            // COMPATIBILIDAD
            // =====================================================

            if (IsMissingValue(transform.skewX))
            {
                transform.skewX =
                    ReadValue(
                        element,
                        "skewX"
                    );
            }

            if (IsMissingValue(transform.skewY))
            {
                transform.skewY =
                    ReadValue(
                        element,
                        "skewY"
                    );
            }

            if (IsMissingValue(transform.scaleX))
            {
                transform.scaleX =
                    ReadValue(
                        element,
                        "scaleX"
                    );
            }

            if (IsMissingValue(transform.scaleY))
            {
                transform.scaleY =
                    ReadValue(
                        element,
                        "scaleY"
                    );
            }

            if (IsMissingValue(transform.frame))
            {
                transform.frame =
                    ReadValue(
                        element,
                        "frame"
                    );
            }

            if (IsMissingValue(transform.alpha))
            {
                transform.alpha =
                    ReadValue(
                        element,
                        "alpha"
                    );
            }

            // =====================================================
            // IMAGEN
            //
            // PvZ real:
            //
            // <i>PeaShooter_Head</i>
            //
            // =====================================================

            string imageName =
                FindFirstString(
                    element,
                    "<i>",
                    "</i>"
                );

            if (string.IsNullOrWhiteSpace(imageName))
            {
                imageName =
                    FindFirstString(
                        element,
                        "<image>",
                        "</image>"
                    );
            }

            if (string.IsNullOrWhiteSpace(imageName))
            {
                imageName =
                    FindAttribute(
                        element,
                        "i"
                    );
            }

            if (!string.IsNullOrWhiteSpace(imageName) &&
                !IsMissingToken(imageName))
            {
                transform.imageName =
                    NormalizeImageName(
                        imageName
                    );
            }

            // =====================================================
            // FONT
            // =====================================================

            string font =
                FindFirstString(
                    element,
                    "<font>",
                    "</font>"
                );

            if (!string.IsNullOrWhiteSpace(font))
            {
                font =
                    CleanValue(font);
            }

            // =====================================================
            // TEXT
            // =====================================================

            string text =
                FindFirstString(
                    element,
                    "<text>",
                    "</text>"
                );

            if (!string.IsNullOrWhiteSpace(text))
            {
                transform.text =
                    CleanValue(text);
            }

            // =====================================================
            // COMPROBAR SI TIENE DATOS
            // =====================================================

            bool hasData =
                !IsMissingValue(transform.x) ||
                !IsMissingValue(transform.y) ||
                !IsMissingValue(transform.skewX) ||
                !IsMissingValue(transform.skewY) ||
                !IsMissingValue(transform.scaleX) ||
                !IsMissingValue(transform.scaleY) ||
                !IsMissingValue(transform.frame) ||
                !IsMissingValue(transform.alpha) ||
                !string.IsNullOrEmpty(
                    transform.imageName
                ) ||
                !string.IsNullOrEmpty(
                    transform.text
                );

            if (!hasData)
            {
                return null;
            }

            return transform;
        }

        // =========================================================
        // XML INDEPENDIENTE
        // =========================================================

        private static void ParseIndependentXmlBlocks(
            string text,
            PvZReanimDefinition definition)
        {
            int position = 0;

            while (true)
            {
                int start =
                    FindOpeningTag(
                        text,
                        "track",
                        position
                    );

                if (start < 0)
                    break;

                int end =
                    FindClosingTag(
                        text,
                        "track",
                        start
                    );

                if (end < 0)
                    break;

                string block =
                    text.Substring(
                        start,
                        end - start +
                        "</track>".Length
                    );

                PvZReanimTrack track =
                    ParseTrackText(
                        block,
                        definition.TrackCount
                    );

                if (track != null)
                {
                    definition.tracks.Add(track);
                }

                position =
                    end +
                    "</track>".Length;
            }
        }

        // =========================================================
        // ENCONTRAR TAG DE APERTURA
        // =========================================================

        private static int FindOpeningTag(
            string text,
            string tag,
            int startIndex)
        {
            if (string.IsNullOrEmpty(text) ||
                string.IsNullOrEmpty(tag))
            {
                return -1;
            }

            int position =
                startIndex;

            while (position < text.Length)
            {
                int index =
                    text.IndexOf(
                        "<" + tag,
                        position,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (index < 0)
                    return -1;

                int afterTag =
                    index +
                    tag.Length +
                    1;

                if (afterTag >= text.Length)
                    return index;

                char c =
                    text[afterTag];

                // Evita confundir:
                //
                // <t>
                //
                // con:
                //
                // <track>
                //
                if (char.IsWhiteSpace(c) ||
                    c == '>' ||
                    c == '/')
                {
                    return index;
                }

                position =
                    afterTag;
            }

            return -1;
        }

        // =========================================================
        // ENCONTRAR CIERRE DE ELEMENTO
        // =========================================================

        private static int FindClosingTag(
            string text,
            string tag,
            int openingStart)
        {
            if (string.IsNullOrEmpty(text))
                return -1;

            string openPrefix =
                "<" + tag;

            string closeTag =
                "</" + tag + ">";

            int openEnd =
                text.IndexOf(
                    '>',
                    openingStart
                );

            if (openEnd < 0)
                return -1;

            // Elemento self-closing:
            //
            // <t ... />
            //
            string opening =
                text.Substring(
                    openingStart,
                    openEnd - openingStart + 1
                );

            if (opening.TrimEnd().EndsWith(
                    "/>",
                    StringComparison.Ordinal))
            {
                return openEnd;
            }

            int depth = 1;
            int position = openEnd + 1;

            while (position < text.Length)
            {
                int nextOpen =
                    FindOpeningTag(
                        text,
                        tag,
                        position
                    );

                int nextClose =
                    text.IndexOf(
                        closeTag,
                        position,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (nextClose < 0)
                    return -1;

                // Si hay otro tag de apertura antes
                // del cierre, incrementamos profundidad.
                if (nextOpen >= 0 &&
                    nextOpen < nextClose)
                {
                    int nextOpenEnd =
                        text.IndexOf(
                            '>',
                            nextOpen
                        );

                    if (nextOpenEnd < 0)
                        return -1;

                    string nestedOpening =
                        text.Substring(
                            nextOpen,
                            nextOpenEnd -
                            nextOpen +
                            1
                        );

                    if (!nestedOpening
                            .TrimEnd()
                            .EndsWith(
                                "/>",
                                StringComparison.Ordinal))
                    {
                        depth++;
                    }

                    position =
                        nextOpenEnd + 1;

                    continue;
                }

                depth--;

                if (depth == 0)
                {
                    return nextClose;
                }

                position =
                    nextClose +
                    closeTag.Length;
            }

            return -1;
        }

        // =========================================================
        // LEER FLOAT
        // =========================================================

        private static float ReadValue(
            string element,
            string name)
        {
            string value =
                FindFirstString(
                    element,
                    "<" + name + ">",
                    "</" + name + ">"
                );

            if (string.IsNullOrWhiteSpace(value))
            {
                value =
                    FindAttribute(
                        element,
                        name
                    );
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return PvZReanimConstants.MissingValue;
            }

            value =
                CleanValue(value);

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

        // =========================================================
        // BUSCAR FLOAT
        // =========================================================

        private static float FindFirstFloat(
            string text,
            string open,
            string close)
        {
            string value =
                FindFirstString(
                    text,
                    open,
                    close
                );

            if (string.IsNullOrWhiteSpace(value))
            {
                return PvZReanimConstants.MissingValue;
            }

            float result;

            if (float.TryParse(
                    CleanValue(value),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
            {
                return result;
            }

            return PvZReanimConstants.MissingValue;
        }

        // =========================================================
        // BUSCAR STRING
        // =========================================================

        private static string FindFirstString(
            string text,
            string open,
            string close)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            int start =
                IndexOfIgnoreCase(
                    text,
                    open
                );

            if (start < 0)
                return null;

            start +=
                open.Length;

            int end =
                IndexOfIgnoreCase(
                    text,
                    close,
                    start
                );

            if (end < 0)
                return null;

            return text.Substring(
                start,
                end - start
            );
        }

        // =========================================================
        // BUSCAR ATRIBUTO
        // =========================================================

        private static string FindAttribute(
            string text,
            string attribute)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                string.IsNullOrWhiteSpace(attribute))
            {
                return null;
            }

            string[] patterns =
            {
                attribute + "=\"",
                attribute + "='"
            };

            for (int i = 0;
                 i < patterns.Length;
                 i++)
            {
                int start =
                    IndexOfIgnoreCase(
                        text,
                        patterns[i]
                    );

                if (start < 0)
                    continue;

                start +=
                    patterns[i].Length;

                char quote =
                    patterns[i][
                        patterns[i].Length - 1
                    ];

                int end =
                    text.IndexOf(
                        quote,
                        start
                    );

                if (end < 0)
                    continue;

                return text.Substring(
                    start,
                    end - start
                );
            }

            return null;
        }

        // =========================================================
        // INDEX IGNORE CASE
        // =========================================================

        private static int IndexOfIgnoreCase(
            string source,
            string value,
            int startIndex = 0)
        {
            if (string.IsNullOrEmpty(source) ||
                string.IsNullOrEmpty(value))
            {
                return -1;
            }

            return source.IndexOf(
                value,
                startIndex,
                StringComparison.OrdinalIgnoreCase
            );
        }

        // =========================================================
        // LIMPIAR
        // =========================================================

        private static string CleanValue(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value =
                value.Trim();

            value =
                value.Replace(
                    "&quot;",
                    "\""
                );

            value =
                value.Replace(
                    "&apos;",
                    "'"
                );

            value =
                value.Replace(
                    "&amp;",
                    "&"
                );

            value =
                value.Replace(
                    "&lt;",
                    "<"
                );

            value =
                value.Replace(
                    "&gt;",
                    ">"
                );

            value =
                Unquote(value);

            return value.Trim();
        }

        // =========================================================
        // QUOTES
        // =========================================================

        private static string Unquote(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length >= 2)
            {
                char first =
                    value[0];

                char last =
                    value[value.Length - 1];

                if ((first == '"' &&
                     last == '"') ||
                    (first == '\'' &&
                     last == '\''))
                {
                    return value.Substring(
                        1,
                        value.Length - 2
                    );
                }
            }

            return value;
        }

        // =========================================================
        // NORMALIZAR IMAGEN
        // =========================================================

        private static string NormalizeImageName(
            string imageName)
        {
            imageName =
                CleanValue(imageName);

            if (string.IsNullOrWhiteSpace(imageName))
                return null;

            imageName =
                imageName.Replace(
                    '\\',
                    '/'
                );

            return imageName;
        }

        // =========================================================
        // TOKEN FALTANTE
        // =========================================================

        private static bool IsMissingToken(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string normalized =
                value.Trim();

            return
                normalized == "-1" ||
                normalized == "-1.0" ||
                normalized == "null" ||
                normalized == "NULL" ||
                normalized == "none" ||
                normalized == "None";
        }

        // =========================================================
        // FLOAT FALTANTE
        // =========================================================

        private static bool IsMissingValue(
            float value)
        {
            return
                value ==
                PvZReanimConstants.MissingValue;
        }
    }
}
