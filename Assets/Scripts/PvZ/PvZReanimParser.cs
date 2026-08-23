using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
                    "La ruta del archivo .reanim est� vac�a.",
                    nameof(path)
                );
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "No se encontr� el archivo .reanim.",
                    path
                );
            }

            return LoadBytes(
                File.ReadAllBytes(path)
            );
        }

        // =========================================================
        // CARGAR BYTES
        // =========================================================

        public static PvZReanimDefinition LoadBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException(
                    "Los datos del .reanim est�n vac�os.",
                    nameof(data)
                );
            }

            string text =
                DecodeText(data);

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
                    "El contenido del .reanim est� vac�o.",
                    nameof(text)
                );
            }

            PvZReanimDefinition definition =
                ParsePvZReanimText(text);

            if (definition != null)
            {
                Debug.Log(
                    "[PvZReanimParser] Reanim parseado correctamente | " +
                    "FPS: " + definition.fps +
                    " | Tracks: " + definition.TrackCount +
                    " | Frames: " +
                    definition.GetMaxFrameCount()
                );

                ValidateTrackFrameCounts(
                    definition
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

            text =
                NormalizeText(text);

            /*
             * Los .reanim originales de PvZ pueden venir:
             *
             * <track>...</track>
             * <track>...</track>
             *
             * o dentro de un elemento ra�z.
             *
             * Envolvemos todo en un <root> artificial para
             * permitir m�ltiples elementos ra�z.
             */

            text =
                RemoveXmlDeclaration(text);

            text =
                RemoveDoctype(text);

            XmlDocument document =
                new XmlDocument();

            try
            {
                document.LoadXml(
                    "<root>" +
                    text +
                    "</root>"
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PvZReanimParser] Error parseando XML: " +
                    exception.Message
                );

                return definition;
            }

            // =====================================================
            // FPS
            // =====================================================

            XmlNode fpsNode =
                document.SelectSingleNode(
                    "//fps"
                );

            if (fpsNode != null)
            {
                float fps;

                if (TryParseFloat(
                        fpsNode.InnerText,
                        out fps
                    ) &&
                    fps > 0f)
                {
                    definition.fps =
                        fps;
                }
            }

            // =====================================================
            // TRACKS
            // =====================================================

            XmlNodeList trackNodes =
                document.SelectNodes(
                    "//track"
                );

            if (trackNodes == null)
            {
                return definition;
            }

            for (int i = 0;
                 i < trackNodes.Count;
                 i++)
            {
                XmlNode trackNode =
                    trackNodes[i];

                PvZReanimTrack track =
                    ParseTrackNode(
                        trackNode,
                        definition.TrackCount
                    );

                if (track != null)
                {
                    definition.tracks.Add(
                        track
                    );
                }
            }

            return definition;
        }

        // =========================================================
        // TRACK
        // =========================================================

        private static PvZReanimTrack ParseTrackNode(
            XmlNode trackNode,
            int index)
        {
            if (trackNode == null)
            {
                return null;
            }

            string trackName =
                GetChildText(
                    trackNode,
                    "name"
                );

            if (string.IsNullOrWhiteSpace(trackName))
            {
                trackName =
                    GetAttribute(
                        trackNode,
                        "name"
                    );
            }

            if (string.IsNullOrWhiteSpace(trackName))
            {
                trackName =
                    "track_" + index;
            }

            trackName =
                CleanValue(
                    trackName
                );

            PvZReanimTrack track =
                new PvZReanimTrack(
                    trackName
                );

            // =====================================================
            // TRANSFORMS OFICIALES
            // =====================================================

            XmlNodeList transformNodes =
                trackNode.SelectNodes(
                    "./t"
                );

            if (transformNodes != null)
            {
                for (int i = 0;
                     i < transformNodes.Count;
                     i++)
                {
                    /*
                     * IMPORTANTE:
                     *
                     * NUNCA descartamos un <t>.
                     *
                     * Un <t> vac�o es v�lido en PvZ.
                     *
                     * Resodded conserva ese frame y despu�s
                     * rellena sus valores con los del frame
                     * anterior.
                     */

                    PvZReanimTransform transform =
                        ParseTransformNode(
                            transformNodes[i]
                        );

                    if (transform == null)
                    {
                        transform =
                            new PvZReanimTransform();
                    }

                    track.transforms.Add(
                        transform
                    );
                }
            }

            // =====================================================
            // COMPATIBILIDAD <transform>
            // =====================================================

            if (track.TransformCount == 0)
            {
                XmlNodeList compatibilityNodes =
                    trackNode.SelectNodes(
                        "./transform"
                    );

                if (compatibilityNodes != null)
                {
                    for (int i = 0;
                         i < compatibilityNodes.Count;
                         i++)
                    {
                        PvZReanimTransform transform =
                            ParseTransformNode(
                                compatibilityNodes[i]
                            );

                        if (transform == null)
                        {
                            transform =
                                new PvZReanimTransform();
                        }

                        track.transforms.Add(
                            transform
                        );
                    }
                }
            }

            /*
             * Resodded rellena los valores inexistentes
             * usando el valor anterior.
             *
             * Tambi�n conserva el frame vac�o.
             */

            PvZReanimDataFiller.FillTrack(
                track
            );

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

        private static PvZReanimTransform ParseTransformNode(
            XmlNode node)
        {
            if (node == null)
            {
                return null;
            }

            PvZReanimTransform transform =
                new PvZReanimTransform();

            // =====================================================
            // POSICI�N
            // =====================================================

            transform.x =
                ReadValue(
                    node,
                    "x"
                );

            transform.y =
                ReadValue(
                    node,
                    "y"
                );

            // =====================================================
            // SKEW
            // =====================================================

            transform.skewX =
                ReadValue(
                    node,
                    "kx"
                );

            transform.skewY =
                ReadValue(
                    node,
                    "ky"
                );

            // Compatibilidad
            if (IsMissingValue(
                    transform.skewX
                ))
            {
                transform.skewX =
                    ReadValue(
                        node,
                        "skewX"
                    );
            }

            if (IsMissingValue(
                    transform.skewY
                ))
            {
                transform.skewY =
                    ReadValue(
                        node,
                        "skewY"
                    );
            }

            // =====================================================
            // SCALE
            // =====================================================

            transform.scaleX =
                ReadValue(
                    node,
                    "sx"
                );

            transform.scaleY =
                ReadValue(
                    node,
                    "sy"
                );

            if (IsMissingValue(
                    transform.scaleX
                ))
            {
                transform.scaleX =
                    ReadValue(
                        node,
                        "scaleX"
                    );
            }

            if (IsMissingValue(
                    transform.scaleY
                ))
            {
                transform.scaleY =
                    ReadValue(
                        node,
                        "scaleY"
                    );
            }

            // =====================================================
            // FRAME
            // =====================================================

            transform.frame =
                ReadValue(
                    node,
                    "f"
                );

            if (IsMissingValue(
                    transform.frame
                ))
            {
                transform.frame =
                    ReadValue(
                        node,
                        "frame"
                    );
            }

            // =====================================================
            // ALPHA
            // =====================================================

            transform.alpha =
                ReadValue(
                    node,
                    "a"
                );

            if (IsMissingValue(
                    transform.alpha
                ))
            {
                transform.alpha =
                    ReadValue(
                        node,
                        "alpha"
                    );
            }

            // =====================================================
            // IMAGE
            // =====================================================

            string imageName =
                GetChildText(
                    node,
                    "i"
                );

            if (string.IsNullOrWhiteSpace(
                    imageName
                ))
            {
                imageName =
                    GetChildText(
                        node,
                        "image"
                    );
            }

            if (string.IsNullOrWhiteSpace(
                    imageName
                ))
            {
                imageName =
                    GetAttribute(
                        node,
                        "i"
                    );
            }

            if (!string.IsNullOrWhiteSpace(
                    imageName
                ) &&
                !IsMissingToken(
                    imageName
                ))
            {
                transform.imageName =
                    NormalizeImageName(
                        imageName
                    );
            }

            // =====================================================
            // FONT
            // =====================================================

            string fontName =
                GetChildText(
                    node,
                    "font"
                );

            if (!string.IsNullOrWhiteSpace(
                    fontName
                ))
            {
                transform.fontName =
                    CleanValue(
                        fontName
                    );
            }

            // =====================================================
            // TEXT
            // =====================================================

            string text =
                GetChildText(
                    node,
                    "text"
                );

            if (!string.IsNullOrWhiteSpace(
                    text
                ))
            {
                transform.text =
                    CleanValue(
                        text
                    );
            }

            /*
             * IMPORTANTE:
             *
             * Aunque no tenga ning�n valor, el transform
             * sigue siendo v�lido.
             *
             * Esto reproduce el comportamiento de
             * ReanimatorTransformConstructor de Resodded.
             */

            return transform;
        }

        // =========================================================
        // XML HELPERS
        // =========================================================

        private static string GetChildText(
            XmlNode node,
            string name)
        {
            if (node == null)
                return null;

            XmlNode child =
                node.SelectSingleNode(
                    "./" + name
                );

            if (child == null)
                return null;

            return CleanValue(
                child.InnerText
            );
        }

        private static string GetAttribute(
            XmlNode node,
            string name)
        {
            if (node == null ||
                node.Attributes == null)
            {
                return null;
            }

            XmlAttribute attribute =
                node.Attributes[name];

            if (attribute == null)
                return null;

            return CleanValue(
                attribute.Value
            );
        }

        private static float ReadValue(
            XmlNode node,
            string name)
        {
            string value =
                GetChildText(
                    node,
                    name
                );

            if (string.IsNullOrWhiteSpace(
                    value
                ))
            {
                value =
                    GetAttribute(
                        node,
                        name
                    );
            }

            if (string.IsNullOrWhiteSpace(
                    value
                ))
            {
                return PvZReanimConstants.MissingValue;
            }

            float result;

            if (TryParseFloat(
                    value,
                    out result
                ))
            {
                return result;
            }

            return PvZReanimConstants.MissingValue;
        }

        private static bool TryParseFloat(
            string value,
            out float result)
        {
            return float.TryParse(
                CleanValue(value),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result
            );
        }

        // =========================================================
        // TEXTO
        // =========================================================

        private static string NormalizeText(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }

        private static string RemoveXmlDeclaration(
            string text)
        {
            return Regex.Replace(
                text,
                @"<\?xml[\s\S]*?\?>",
                "",
                RegexOptions.IgnoreCase
            );
        }

        private static string RemoveDoctype(
            string text)
        {
            return Regex.Replace(
                text,
                @"<!DOCTYPE[\s\S]*?>",
                "",
                RegexOptions.IgnoreCase
            );
        }

        private static string CleanValue(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value
                ))
            {
                return null;
            }

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
            if (string.IsNullOrEmpty(
                    value
                ))
            {
                return value;
            }

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
        // IMAGE
        // =========================================================

        private static string NormalizeImageName(
            string imageName)
        {
            imageName =
                CleanValue(
                    imageName
                );

            if (string.IsNullOrWhiteSpace(
                    imageName
                ))
            {
                return null;
            }

            imageName =
                imageName.Replace(
                    '\\',
                    '/'
                );

            return imageName;
        }

        // =========================================================
        // MISSING
        // =========================================================

        private static bool IsMissingToken(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value
                ))
            {
                return true;
            }

            string normalized =
                value.Trim();

            return
                normalized == "-1" ||
                normalized == "-1.0" ||
                normalized.Equals(
                    "null",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                normalized.Equals(
                    "none",
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                   PvZReanimConstants.MissingValue;
        }

        // =========================================================
        // VALIDACI�N
        // =========================================================

        private static void ValidateTrackFrameCounts(
            PvZReanimDefinition definition)
        {
            if (definition == null ||
                definition.TrackCount == 0)
            {
                return;
            }

            int expected =
                definition.GetTrack(0)
                    .TransformCount;

            bool valid = true;

            for (int i = 0;
                 i < definition.TrackCount;
                 i++)
            {
                PvZReanimTrack track =
                    definition.GetTrack(i);

                if (track == null)
                    continue;

                if (track.TransformCount != expected)
                {
                    valid = false;

                    Debug.LogWarning(
                        "[PvZReanimParser] Track '" +
                        track.name +
                        "' tiene " +
                        track.TransformCount +
                        " frames; se esperaban " +
                        expected +
                        "."
                    );
                }
            }

            if (valid)
            {
                Debug.Log(
                    "[PvZReanimParser] Todos los tracks tienen " +
                    expected +
                    " frames."
                );
            }
        }
    }
}