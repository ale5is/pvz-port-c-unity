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

        // =========================================================
        // CARGAR BYTES
        // =========================================================

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

            /*
             * Los .reanim originales de PvZ son XML.
             *
             * No utilizamos Encoding.UTF8.GetString()
             * directamente como único sistema de lectura,
             * porque algunos archivos pueden contener
             * información de encoding en la cabecera XML.
             */

            string text =
                DecodeText(data);

            return Parse(text);
        }

        // =========================================================
        // DECODIFICAR TEXTO
        // =========================================================

        private static string DecodeText(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                return string.Empty;
            }

            /*
             * UTF-8 BOM
             */
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

            /*
             * UTF-16 LE BOM
             */
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

            /*
             * UTF-16 BE BOM
             */
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

            return Encoding.UTF8.GetString(
                data
            );
        }

        // =========================================================
        // PARSER PRINCIPAL
        // =========================================================

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

            /*
             * Primero intentamos el formato XML real
             * utilizado por los Reanim de PvZ.
             */

            try
            {
                PvZReanimDefinition definition =
                    ParseXml(text);

                if (definition != null &&
                    definition.TrackCount > 0)
                {
                    return definition;
                }

                /*
                 * Si XML pudo abrirse pero no encontró
                 * tracks, probamos el parser antiguo
                 * como compatibilidad.
                 */
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PvZReanimParser] " +
                    "Error leyendo XML: " +
                    exception.Message
                );
            }

            /*
             * Compatibilidad con el formato de texto
             * simplificado utilizado por versiones
             * anteriores del proyecto.
             */

            return ParseLegacyText(
                text
            );
        }

        // =========================================================
        // XML REAL DE PVZ
        // =========================================================

        private static PvZReanimDefinition ParseXml(
            string text)
        {
            XmlDocument document =
                new XmlDocument();

            document.XmlResolver = null;

            document.LoadXml(
                text
            );

            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<
                    PvZReanimDefinition
                >();

            /*
             * Buscar FPS en cualquier nivel.
             */

            XmlNode fpsNode =
                FindFirstElement(
                    document.DocumentElement,
                    "fps"
                );

            if (fpsNode != null)
            {
                float fps =
                    ParseFloat(
                        fpsNode.InnerText
                    );

                if (!IsMissingValue(fps))
                {
                    definition.fps =
                        fps;
                }
            }

            /*
             * Los tracks pueden encontrarse
             * directamente debajo de la raíz.
             *
             * También buscamos recursivamente
             * por seguridad.
             */

            List<XmlNode> trackNodes =
                new List<XmlNode>();

            FindElements(
                document.DocumentElement,
                "track",
                trackNodes
            );

            for (int i = 0;
                 i < trackNodes.Count;
                 i++)
            {
                ParseXmlTrack(
                    definition,
                    trackNodes[i]
                );
            }

            /*
             * Nombre opcional de la definición.
             *
             * No siempre está presente en el XML,
             * por eso el RuntimeLoader lo completa
             * usando el nombre del archivo.
             */

            XmlNode nameNode =
                FindFirstElement(
                    document.DocumentElement,
                    "name"
                );

            if (nameNode != null)
            {
                /*
                 * El primer <name> puede pertenecer
                 * a un track. No lo usamos como nombre
                 * global si ya tenemos tracks.
                 */

                if (definition.TrackCount == 0)
                {
                    /*
                     * No hay campo name en
                     * PvZReanimDefinition actualmente,
                     * así que no hacemos nada aquí.
                     */
                }
            }

            Debug.Log(
                "[PvZReanimParser] XML parseado | " +
                "FPS: " +
                definition.fps +
                " | Tracks: " +
                definition.TrackCount +
                " | Frames: " +
                definition.GetMaxFrameCount()
            );

            return definition;
        }

        // =========================================================
        // TRACK XML
        // =========================================================

        private static void ParseXmlTrack(
            PvZReanimDefinition definition,
            XmlNode trackNode)
        {
            if (definition == null ||
                trackNode == null)
            {
                return;
            }

            string trackName =
                GetChildValue(
                    trackNode,
                    "name"
                );

            if (string.IsNullOrEmpty(trackName))
            {
                /*
                 * Algunos formatos pueden guardar
                 * el nombre como atributo.
                 */

                trackName =
                    GetAttribute(
                        trackNode,
                        "name"
                    );
            }

            if (string.IsNullOrEmpty(trackName))
            {
                trackName =
                    "track_" +
                    definition.TrackCount;
            }

            PvZReanimTrack track =
                new PvZReanimTrack(
                    trackName
                );

            definition.tracks.Add(
                track
            );

            /*
             * Cada <t> representa un frame/keyframe
             * del track.
             */

            List<XmlNode> transformNodes =
                new List<XmlNode>();

            FindDirectElements(
                trackNode,
                "t",
                transformNodes
            );

            /*
             * Compatibilidad:
             * algunas variantes pueden utilizar
             * "transform".
             */

            if (transformNodes.Count == 0)
            {
                FindDirectElements(
                    trackNode,
                    "transform",
                    transformNodes
                );
            }

            for (int i = 0;
                 i < transformNodes.Count;
                 i++)
            {
                PvZReanimTransform transform =
                    ParseXmlTransform(
                        transformNodes[i]
                    );

                if (transform != null)
                {
                    track.transforms.Add(
                        transform
                    );
                }
            }

            Debug.Log(
                "[PvZReanimParser] Track: " +
                track.name +
                " | Frames: " +
                track.TransformCount
            );
        }

        // =========================================================
        // TRANSFORM XML
        // =========================================================

        private static PvZReanimTransform ParseXmlTransform(
            XmlNode node)
        {
            if (node == null)
            {
                return null;
            }

            PvZReanimTransform transform =
                new PvZReanimTransform();

            /*
             * Valores por defecto.
             */

            transform.x =
                PvZReanimConstants.MissingValue;

            transform.y =
                PvZReanimConstants.MissingValue;

            transform.skewX =
                PvZReanimConstants.MissingValue;

            transform.skewY =
                PvZReanimConstants.MissingValue;

            transform.scaleX =
                PvZReanimConstants.MissingValue;

            transform.scaleY =
                PvZReanimConstants.MissingValue;

            transform.frame =
                PvZReanimConstants.MissingValue;

            transform.alpha =
                PvZReanimConstants.MissingValue;

            transform.imageName =
                null;

            transform.text =
                null;

            /*
             * POSITION
             */

            transform.x =
                ReadProperty(
                    node,
                    "x",
                    PvZReanimConstants.MissingValue
                );

            transform.y =
                ReadProperty(
                    node,
                    "y",
                    PvZReanimConstants.MissingValue
                );

            /*
             * SKEW
             *
             * PvZ utiliza kx / ky.
             *
             * El proyecto Unity utiliza
             * skewX / skewY.
             */

            transform.skewX =
                ReadProperty(
                    node,
                    "kx",
                    PvZReanimConstants.MissingValue
                );

            transform.skewY =
                ReadProperty(
                    node,
                    "ky",
                    PvZReanimConstants.MissingValue
                );

            /*
             * Algunas variantes pueden escribir
             * directamente skewX / skewY.
             */

            if (IsMissingValue(
                    transform.skewX))
            {
                transform.skewX =
                    ReadProperty(
                        node,
                        "skewX",
                        PvZReanimConstants.MissingValue
                    );
            }

            if (IsMissingValue(
                    transform.skewY))
            {
                transform.skewY =
                    ReadProperty(
                        node,
                        "skewY",
                        PvZReanimConstants.MissingValue
                    );
            }

            /*
             * SCALE
             */

            transform.scaleX =
                ReadProperty(
                    node,
                    "sx",
                    PvZReanimConstants.MissingValue
                );

            transform.scaleY =
                ReadProperty(
                    node,
                    "sy",
                    PvZReanimConstants.MissingValue
                );

            /*
             * Compatibilidad con scaleX / scaleY.
             */

            if (IsMissingValue(
                    transform.scaleX))
            {
                transform.scaleX =
                    ReadProperty(
                        node,
                        "scaleX",
                        PvZReanimConstants.MissingValue
                    );
            }

            if (IsMissingValue(
                    transform.scaleY))
            {
                transform.scaleY =
                    ReadProperty(
                        node,
                        "scaleY",
                        PvZReanimConstants.MissingValue
                    );
            }

            /*
             * FRAME
             *
             * En Reanim:
             *
             * f = frame / image frame
             */

            transform.frame =
                ReadProperty(
                    node,
                    "f",
                    PvZReanimConstants.MissingValue
                );

            if (IsMissingValue(
                    transform.frame))
            {
                transform.frame =
                    ReadProperty(
                        node,
                        "frame",
                        PvZReanimConstants.MissingValue
                    );
            }

            /*
             * ALPHA
             */

            transform.alpha =
                ReadProperty(
                    node,
                    "a",
                    PvZReanimConstants.MissingValue
                );

            if (IsMissingValue(
                    transform.alpha))
            {
                transform.alpha =
                    ReadProperty(
                        node,
                        "alpha",
                        PvZReanimConstants.MissingValue
                    );
            }

            /*
             * IMAGE
             *
             * En los Reanim originales:
             *
             * i = nombre de imagen
             */

            string imageName =
                GetChildValue(
                    node,
                    "i"
                );

            if (string.IsNullOrEmpty(
                    imageName))
            {
                imageName =
                    GetChildValue(
                        node,
                        "image"
                    );
            }

            if (string.IsNullOrEmpty(
                    imageName))
            {
                imageName =
                    GetAttribute(
                        node,
                        "i"
                    );
            }

            if (!string.IsNullOrEmpty(
                    imageName) &&
                !IsMissingToken(
                    imageName))
            {
                transform.imageName =
                    NormalizeImageName(
                        imageName
                    );
            }

            /*
             * TEXT
             */

            string text =
                GetChildValue(
                    node,
                    "text"
                );

            if (string.IsNullOrEmpty(
                    text))
            {
                text =
                    GetChildValue(
                        node,
                        "font"
                    );
            }

            if (!string.IsNullOrEmpty(
                    text) &&
                !IsMissingToken(
                    text))
            {
                transform.text =
                    text;
            }

            return transform;
        }

        // =========================================================
        // NORMALIZAR IMAGEN
        // =========================================================

        private static string NormalizeImageName(
            string imageName)
        {
            if (string.IsNullOrWhiteSpace(
                    imageName))
            {
                return null;
            }

            imageName =
                imageName.Trim();

            imageName =
                Unquote(
                    imageName
                );

            if (IsMissingToken(
                    imageName))
            {
                return null;
            }

            imageName =
                imageName.Replace(
                    '\\',
                    '/'
                );

            /*
             * Si el Reanim guarda solamente:
             *
             * PeaShooter_Head
             *
             * el resolver del proyecto se encargará
             * de buscar el PNG.
             *
             * Si ya contiene .png lo conservamos.
             */

            return imageName;
        }

        // =========================================================
        // READ PROPERTY
        // =========================================================

        private static float ReadProperty(
            XmlNode parent,
            string name,
            float fallback)
        {
            if (parent == null)
            {
                return fallback;
            }

            string value =
                GetChildValue(
                    parent,
                    name
                );

            if (string.IsNullOrEmpty(
                    value))
            {
                value =
                    GetAttribute(
                        parent,
                        name
                    );
            }

            if (string.IsNullOrEmpty(
                    value))
            {
                return fallback;
            }

            float result =
                ParseFloat(
                    value
                );

            if (IsMissingValue(
                    result))
            {
                return fallback;
            }

            return result;
        }

        // =========================================================
        // FIND FIRST ELEMENT
        // =========================================================

        private static XmlNode FindFirstElement(
            XmlNode parent,
            string elementName)
        {
            if (parent == null)
            {
                return null;
            }

            if (string.Equals(
                    parent.Name,
                    elementName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }

            for (XmlNode child =
                    parent.FirstChild;
                 child != null;
                 child =
                    child.NextSibling)
            {
                if (child.NodeType !=
                    XmlNodeType.Element)
                {
                    continue;
                }

                XmlNode result =
                    FindFirstElement(
                        child,
                        elementName
                    );

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        // =========================================================
        // FIND ELEMENTS
        // =========================================================

        private static void FindElements(
            XmlNode parent,
            string elementName,
            List<XmlNode> result)
        {
            if (parent == null ||
                result == null)
            {
                return;
            }

            if (parent.NodeType ==
                    XmlNodeType.Element &&
                string.Equals(
                    parent.Name,
                    elementName,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    parent
                );
            }

            for (XmlNode child =
                    parent.FirstChild;
                 child != null;
                 child =
                    child.NextSibling)
            {
                if (child.NodeType !=
                    XmlNodeType.Element)
                {
                    continue;
                }

                FindElements(
                    child,
                    elementName,
                    result
                );
            }
        }

        // =========================================================
        // FIND DIRECT ELEMENTS
        // =========================================================

        private static void FindDirectElements(
            XmlNode parent,
            string elementName,
            List<XmlNode> result)
        {
            if (parent == null ||
                result == null)
            {
                return;
            }

            for (XmlNode child =
                    parent.FirstChild;
                 child != null;
                 child =
                    child.NextSibling)
            {
                if (child.NodeType !=
                    XmlNodeType.Element)
                {
                    continue;
                }

                if (string.Equals(
                        child.Name,
                        elementName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(
                        child
                    );
                }
            }
        }

        // =========================================================
        // GET CHILD VALUE
        // =========================================================

        private static string GetChildValue(
            XmlNode parent,
            string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (XmlNode child =
                    parent.FirstChild;
                 child != null;
                 child =
                    child.NextSibling)
            {
                if (child.NodeType !=
                    XmlNodeType.Element)
                {
                    continue;
                }

                if (string.Equals(
                        child.Name,
                        childName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child.InnerText.Trim();
                }
            }

            return null;
        }

        // =========================================================
        // GET ATTRIBUTE
        // =========================================================

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
            {
                return null;
            }

            return attribute.Value;
        }

        // =========================================================
        // PARSER LEGACY
        // =========================================================

        private static PvZReanimDefinition ParseLegacyText(
            string text)
        {
            string normalized =
                text
                    .Replace(
                        "\r\n",
                        "\n"
                    )
                    .Replace(
                        '\r',
                        '\n'
                    );

            string[] lines =
                normalized.Split(
                    '\n'
                );

            PvZReanimDefinition definition =
                ScriptableObject.CreateInstance<
                    PvZReanimDefinition
                >();

            PvZReanimTrack currentTrack =
                null;

            for (int i = 0;
                 i < lines.Length;
                 i++)
            {
                string line =
                    RemoveComment(
                        lines[i]
                    ).Trim();

                if (string.IsNullOrEmpty(
                        line))
                {
                    continue;
                }

                string[] tokens =
                    Tokenize(
                        line
                    );

                if (tokens.Length == 0)
                {
                    continue;
                }

                string command =
                    tokens[0]
                        .Trim()
                        .ToLowerInvariant();

                switch (command)
                {
                    case "track":

                        currentTrack =
                            ParseLegacyTrack(
                                definition,
                                tokens
                            );

                        break;

                    case "transform":

                        if (currentTrack == null)
                        {
                            Debug.LogWarning(
                                "[PvZReanimParser] " +
                                "transform sin track " +
                                "en línea " +
                                (i + 1)
                            );

                            continue;
                        }

                        ParseLegacyTransform(
                            currentTrack,
                            tokens
                        );

                        break;

                    default:

                        ParseLegacyGlobalProperty(
                            definition,
                            tokens
                        );

                        break;
                }
            }

            Debug.Log(
                "[PvZReanimParser] " +
                "Legacy parseado | " +
                "FPS: " +
                definition.fps +
                " | Tracks: " +
                definition.TrackCount +
                " | Frames: " +
                definition.GetMaxFrameCount()
            );

            return definition;
        }

        // =========================================================
        // LEGACY TRACK
        // =========================================================

        private static PvZReanimTrack ParseLegacyTrack(
            PvZReanimDefinition definition,
            string[] tokens)
        {
            string name =
                tokens.Length > 1
                    ? tokens[1]
                    : "track_" +
                      definition.TrackCount;

            PvZReanimTrack track =
                new PvZReanimTrack(
                    Unquote(
                        name
                    )
                );

            definition.tracks.Add(
                track
            );

            return track;
        }

        // =========================================================
        // LEGACY TRANSFORM
        // =========================================================

        private static void ParseLegacyTransform(
            PvZReanimTrack track,
            string[] tokens)
        {
            PvZReanimTransform transform =
                new PvZReanimTransform();

            int index = 1;

            transform.x =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.y =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.skewX =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.skewY =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.scaleX =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.scaleY =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.frame =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            transform.alpha =
                ReadLegacyFloat(
                    tokens,
                    ref index
                );

            if (index <
                tokens.Length)
            {
                string imageName =
                    Unquote(
                        tokens[index++]
                    );

                if (!IsMissingToken(
                        imageName))
                {
                    transform.imageName =
                        NormalizeImageName(
                            imageName
                        );
                }
            }

            if (index <
                tokens.Length)
            {
                string text =
                    Unquote(
                        tokens[index++]
                    );

                if (!IsMissingToken(
                        text))
                {
                    transform.text =
                        text;
                }
            }

            track.transforms.Add(
                transform
            );
        }

        // =========================================================
        // LEGACY FLOAT
        // =========================================================

        private static float ReadLegacyFloat(
            string[] tokens,
            ref int index)
        {
            if (index >=
                tokens.Length)
            {
                return PvZReanimConstants.MissingValue;
            }

            string value =
                tokens[index++];

            return ParseFloat(
                value
            );
        }

        // =========================================================
        // LEGACY GLOBAL
        // =========================================================

        private static void ParseLegacyGlobalProperty(
            PvZReanimDefinition definition,
            string[] tokens)
        {
            if (tokens.Length < 2)
            {
                return;
            }

            string property =
                tokens[0]
                    .Trim()
                    .ToLowerInvariant();

            string value =
                tokens[1];

            switch (property)
            {
                case "fps":
                case "framerate":
                case "rate":

                    float fps =
                        ParseFloat(
                            value
                        );

                    if (!IsMissingValue(
                            fps))
                    {
                        definition.fps =
                            fps;
                    }

                    break;
            }
        }

        // =========================================================
        // FLOAT
        // =========================================================

        private static float ParseFloat(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return PvZReanimConstants.MissingValue;
            }

            value =
                Unquote(
                    value
                );

            if (IsMissingToken(
                    value))
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

        // =========================================================
        // MISSING VALUE
        // =========================================================

        private static bool IsMissingValue(
            float value)
        {
            return value ==
                PvZReanimConstants.MissingValue;
        }

        private static bool IsMissingToken(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return true;
            }

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

        // =========================================================
        // UNQUOTE
        // =========================================================

        private static string Unquote(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return value;
            }

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

        // =========================================================
        // REMOVE COMMENT
        // =========================================================

        private static string RemoveComment(
            string line)
        {
            if (string.IsNullOrEmpty(
                    line))
            {
                return string.Empty;
            }

            bool quoted =
                false;

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

        // =========================================================
        // TOKENIZE
        // =========================================================

        private static string[] Tokenize(
            string line)
        {
            if (string.IsNullOrWhiteSpace(
                    line))
            {
                return Array.Empty<string>();
            }

            List<string> tokens =
                new List<string>();

            StringBuilder current =
                new StringBuilder();

            bool quoted =
                false;

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

                    current.Append(
                        c
                    );

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

                current.Append(
                    c
                );
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
