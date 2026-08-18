using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
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

            /*
             * IMPORTANTE:
             *
             * Los Reanim reales de PvZ pueden contener
             * múltiples bloques XML consecutivos.
             *
             * Por eso NO usamos directamente:
             *
             * document.LoadXml(text)
             *
             * ya que produce:
             *
             * "There are multiple root elements."
             */

            PvZReanimDefinition definition =
                ParsePvZReanimText(text);

            if (definition != null &&
                definition.TrackCount > 0)
            {
                Debug.Log(
                    "[PvZReanimParser] Reanim parseado correctamente | " +
                    "FPS: " + definition.fps +
                    " | Tracks: " + definition.TrackCount +
                    " | Frames: " +
                    definition.GetMaxFrameCount()
                );

                return definition;
            }

            Debug.LogWarning(
                "[PvZReanimParser] " +
                "No se encontraron tracks en el Reanim."
            );

            return definition;
        }

        // =========================================================
        // PARSER PVZ
        // =========================================================

        private static PvZReanimDefinition ParsePvZReanimText(
            string text)
        {
            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<PvZReanimDefinition>();

            definition.fps =
                PvZReanimConstants.DefaultFPS;

            /*
             * Normalizamos saltos.
             */

            text = text.Replace(
                "\r\n",
                "\n"
            );

            text = text.Replace(
                '\r',
                '\n'
            );

            /*
             * Primero intentamos localizar el FPS.
             */

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

            /*
             * El formato real puede tener:
             *
             * <track>
             * ...
             * </track>
             *
             * repetido.
             */

            int searchPosition = 0;

            while (true)
            {
                int trackStart =
                    IndexOfIgnoreCase(
                        text,
                        "<track",
                        searchPosition
                    );

                if (trackStart < 0)
                    break;

                int openEnd =
                    text.IndexOf(
                        '>',
                        trackStart
                    );

                if (openEnd < 0)
                    break;

                int trackEnd =
                    IndexOfIgnoreCase(
                        text,
                        "</track>",
                        openEnd + 1
                    );

                if (trackEnd < 0)
                    break;

                int contentStart =
                    openEnd + 1;

                int contentLength =
                    trackEnd - contentStart;

                if (contentLength < 0)
                    break;

                string trackText =
                    text.Substring(
                        contentStart,
                        contentLength
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

            /*
             * Si no encontramos <track>, intentamos
             * buscar bloques de track mediante XML
             * individualmente.
             */

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

            /*
             * Buscar todos los <t> del track.
             */

            int position = 0;

            while (true)
            {
                int start =
                    IndexOfIgnoreCase(
                        text,
                        "<t",
                        position
                    );

                if (start < 0)
                    break;

                int end =
                    FindElementEnd(
                        text,
                        start
                    );

                if (end < 0)
                    break;

                string element =
                    text.Substring(
                        start,
                        end - start + 1
                    );

                /*
                 * Evitamos confundir:
                 *
                 * <track>
                 *
                 * con:
                 *
                 * <t>
                 */

                if (IsTransformElement(element))
                {
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
                }

                position = end + 1;
            }

            /*
             * Compatibilidad con <transform>.
             */

            if (track.TransformCount == 0)
            {
                position = 0;

                while (true)
                {
                    int start =
                        IndexOfIgnoreCase(
                            text,
                            "<transform",
                            position
                        );

                    if (start < 0)
                        break;

                    int end =
                        FindElementEnd(
                            text,
                            start
                        );

                    if (end < 0)
                        break;

                    string element =
                        text.Substring(
                            start,
                            end - start + 1
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

                    position = end + 1;
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
        // TRANSFORM
        // =========================================================

        private static PvZReanimTransform ParseTransformElement(
            string element)
        {
            if (string.IsNullOrWhiteSpace(element))
                return null;

            PvZReanimTransform transform =
                new PvZReanimTransform();

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

            transform.frame =
                ReadValue(
                    element,
                    "f"
                );

            if (IsMissingValue(transform.frame))
            {
                transform.frame =
                    ReadValue(
                        element,
                        "frame"
                    );
            }

            transform.alpha =
                ReadValue(
                    element,
                    "a"
                );

            if (IsMissingValue(transform.alpha))
            {
                transform.alpha =
                    ReadValue(
                        element,
                        "alpha"
                    );
            }

            /*
             * Imagen.
             *
             * Formato habitual:
             *
             * <i>PeaShooter_Head</i>
             */

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

            /*
             * Texto.
             */

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

            /*
             * Si el elemento no tiene absolutamente
             * ninguna propiedad útil, no lo agregamos.
             */

            bool hasData =
                !IsMissingValue(transform.x) ||
                !IsMissingValue(transform.y) ||
                !IsMissingValue(transform.skewX) ||
                !IsMissingValue(transform.skewY) ||
                !IsMissingValue(transform.scaleX) ||
                !IsMissingValue(transform.scaleY) ||
                !IsMissingValue(transform.frame) ||
                !IsMissingValue(transform.alpha) ||
                !string.IsNullOrEmpty(transform.imageName) ||
                !string.IsNullOrEmpty(transform.text);

            if (!hasData)
            {
                return null;
            }

            return transform;
        }

        // =========================================================
        // PARSE XML INDIVIDUAL
        // =========================================================

        private static void ParseIndependentXmlBlocks(
            string text,
            PvZReanimDefinition definition)
        {
            /*
             * Algunos archivos pueden estar formados
             * por bloques XML independientes.
             *
             * Intentamos localizar <track ...>
             * y convertir solamente ese bloque.
             */

            int position = 0;

            while (true)
            {
                int start =
                    IndexOfIgnoreCase(
                        text,
                        "<track",
                        position
                    );

                if (start < 0)
                    break;

                int end =
                    IndexOfIgnoreCase(
                        text,
                        "</track>",
                        start
                    );

                if (end < 0)
                    break;

                end += "</track>".Length;

                string block =
                    text.Substring(
                        start,
                        end - start
                    );

                int nameStart =
                    IndexOfIgnoreCase(
                        block,
                        "<name>"
                    );

                string name = null;

                if (nameStart >= 0)
                {
                    name =
                        FindFirstString(
                            block,
                            "<name>",
                            "</name>"
                        );
                }

                PvZReanimTrack track =
                    ParseTrackText(
                        block,
                        definition.TrackCount
                    );

                if (track != null)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        track.name =
                            CleanValue(name);
                    }

                    definition.tracks.Add(track);
                }

                position = end;
            }
        }

        // =========================================================
        // HELPERS
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

        private static int FindElementEnd(
            string text,
            int start)
        {
            int end =
                text.IndexOf(
                    '>',
                    start
                );

            if (end < 0)
                return -1;

            return end;
        }

        private static bool IsTransformElement(
            string element)
        {
            if (string.IsNullOrWhiteSpace(element))
                return false;

            string trimmed =
                element.TrimStart();

            if (trimmed.StartsWith(
                    "<track",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmed.StartsWith(
                    "<transform",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.StartsWith(
                    "<t",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

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

            start += open.Length;

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
                    patterns[i][patterns[i].Length - 1];

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
                Unquote(value);

            return value.Trim();
        }

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

                if ((first == '"' && last == '"') ||
                    (first == '\'' && last == '\''))
                {
                    return value.Substring(
                        1,
                        value.Length - 2
                    );
                }
            }

            return value;
        }

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

            /*
             * No añadimos .png automáticamente.
             *
             * El ImageResolver debe decidir cómo
             * encontrar la imagen correspondiente.
             */

            return imageName;
        }

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

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                   PvZReanimConstants.MissingValue;
        }
    }
}
