using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using UnityEngine;

/// <summary>
/// Parser de archivos REANIM de Plants vs Zombies.
///
/// Soporta el formato REANIM XML utilizado por PvZ,
/// incluyendo:
///
/// <track>
///     <name>...</name>
///     <t>
///         <f>...</f>
///         <x>...</x>
///         <y>...</y>
///         <sx>...</sx>
///         <sy>...</sy>
///         <r>...</r>
///         <a>...</a>
///         <i>...</i>
///     </t>
/// </track>
///
/// También soporta archivos que contienen múltiples
/// elementos raíz.
/// </summary>
public static class PvZReanimParser
{
    // ============================================================
    // CONFIGURACIÓN
    // ============================================================

    private const float DEFAULT_SCALE = 1f;

    // ============================================================
    // PARSEAR DESDE BYTES
    // ============================================================

    public static PvZReanimData Parse(
        byte[] datos,
        string nombre = "")
    {
        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ REANIM] Datos vacíos: " +
                nombre);

            return null;
        }

        try
        {
            string texto = Decodificar(datos);

            return Parse(
                texto,
                nombre);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[PvZ REANIM] Error leyendo " +
                nombre +
                ": " +
                ex);

            return null;
        }
    }

    // ============================================================
    // PARSEAR DESDE STRING
    // ============================================================

    public static PvZReanimData Parse(
        string texto,
        string nombre = "")
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            Debug.LogError(
                "[PvZ REANIM] Texto vacío: " +
                nombre);

            return null;
        }

        try
        {
            texto = LimpiarTexto(texto);

            if (texto.Length == 0)
            {
                return null;
            }

            Debug.Log(
                "[PvZ REANIM] Parseando: " +
                nombre +
                " | Caracteres=" +
                texto.Length);

            return ParseXmlFlexible(
                texto,
                nombre);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[PvZ REANIM] Error de parser en " +
                nombre +
                ": " +
                ex);

            return null;
        }
    }

    // ============================================================
    // XML FLEXIBLE
    // ============================================================

    private static PvZReanimData ParseXmlFlexible(
        string texto,
        string nombre)
    {
        PvZReanimData resultado =
            new PvZReanimData();

        // --------------------------------------------------------
        // Intento 1:
        // XML normal con una única raíz.
        // --------------------------------------------------------

        XmlDocument documento =
            new XmlDocument();

        bool xmlNormal = false;

        try
        {
            documento.LoadXml(texto);
            xmlNormal = true;
        }
        catch (XmlException)
        {
            // El REANIM puede contener múltiples raíces.
            // No es un error para nosotros.
        }

        if (xmlNormal)
        {
            ParsearDocumento(
                documento,
                resultado);
        }
        else
        {
            // ----------------------------------------------------
            // Intento 2:
            // Envolver todo en una raíz artificial.
            // ----------------------------------------------------

            string envuelto =
                "<reanim>" +
                texto +
                "</reanim>";

            try
            {
                documento =
                    new XmlDocument();

                documento.LoadXml(envuelto);

                ParsearDocumento(
                    documento,
                    resultado);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PvZ REANIM] No se pudo interpretar el XML: " +
                    nombre +
                    "\n" +
                    ex);

                return null;
            }
        }

        Debug.Log(
            "[PvZ REANIM] Parse terminado: " +
            nombre +
            " | Tracks=" +
            resultado.tracks.Count);

        return resultado;
    }

    // ============================================================
    // PARSEAR DOCUMENTO
    // ============================================================

    private static void ParsearDocumento(
        XmlDocument documento,
        PvZReanimData resultado)
    {
        if (documento == null ||
            resultado == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Buscar tracks reales.
        // --------------------------------------------------------

        XmlNodeList nodosTrack =
            documento.SelectNodes(
                "//track");

        if (nodosTrack == null ||
            nodosTrack.Count == 0)
        {
            nodosTrack =
                documento.SelectNodes(
                    "//Track");
        }

        if (nodosTrack == null ||
            nodosTrack.Count == 0)
        {
            Debug.LogWarning(
                "[PvZ REANIM] No se encontraron tracks.");

            return;
        }

        Debug.Log(
            "[PvZ REANIM] Tracks encontrados: " +
            nodosTrack.Count);

        // --------------------------------------------------------
        // TRACKS
        // --------------------------------------------------------

        int indiceTrack = 0;

        foreach (XmlNode nodoTrack in nodosTrack)
        {
            PvZReanimTrack track =
                ParseTrack(
                    nodoTrack,
                    indiceTrack);

            if (track != null)
            {
                resultado.tracks.Add(track);
            }

            indiceTrack++;
        }
    }

    // ============================================================
    // TRACK
    // ============================================================

    private static PvZReanimTrack ParseTrack(
        XmlNode nodoTrack,
        int indiceTrack)
    {
        if (nodoTrack == null)
        {
            return null;
        }

        PvZReanimTrack track =
            new PvZReanimTrack();

        // ========================================================
        // NOMBRE
        // ========================================================

        string nombre =
            ObtenerValor(
                nodoTrack,
                "name");

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre =
                ObtenerAtributo(
                    nodoTrack,
                    "name");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre =
                ObtenerValor(
                    nodoTrack,
                    "n");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre =
                ObtenerAtributo(
                    nodoTrack,
                    "n");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre =
                "track_" +
                indiceTrack;
        }

        track.name =
            nombre.Trim();

        // ========================================================
        // ESTADO HEREDADO
        // ========================================================

        float ultimoX = 0f;
        float ultimoY = 0f;

        float ultimoSX =
            DEFAULT_SCALE;

        float ultimoSY =
            DEFAULT_SCALE;

        float ultimoF = 0f;

        string ultimaImagen = null;

        // ========================================================
        // BUSCAR FRAMES
        // ========================================================

        List<XmlNode> nodosFrame =
            new List<XmlNode>();

        // --------------------------------------------------------
        // FORMATO PRINCIPAL DE PVZ:
        //
        // <t>
        //     <f>0</f>
        //     <x>...</x>
        //     ...
        // </t>
        // --------------------------------------------------------

        XmlNodeList transformaciones =
            nodoTrack.SelectNodes(
                "./t");

        if (transformaciones != null)
        {
            foreach (XmlNode nodo in transformaciones)
            {
                if (nodo.NodeType ==
                    XmlNodeType.Element)
                {
                    nodosFrame.Add(nodo);
                }
            }
        }

        // --------------------------------------------------------
        // Compatibilidad con <frame>.
        // --------------------------------------------------------

        if (nodosFrame.Count == 0)
        {
            XmlNodeList frames =
                nodoTrack.SelectNodes(
                    "./frame");

            if (frames != null)
            {
                foreach (XmlNode nodo in frames)
                {
                    if (nodo.NodeType ==
                        XmlNodeType.Element)
                    {
                        nodosFrame.Add(nodo);
                    }
                }
            }
        }

        // --------------------------------------------------------
        // Compatibilidad con <f>.
        // --------------------------------------------------------

        if (nodosFrame.Count == 0)
        {
            XmlNodeList frames =
                nodoTrack.SelectNodes(
                    "./f");

            if (frames != null)
            {
                foreach (XmlNode nodo in frames)
                {
                    if (nodo.NodeType ==
                        XmlNodeType.Element)
                    {
                        nodosFrame.Add(nodo);
                    }
                }
            }
        }

        // --------------------------------------------------------
        // Último intento: buscar t/frame recursivamente.
        // --------------------------------------------------------

        if (nodosFrame.Count == 0)
        {
            foreach (XmlNode nodo in nodoTrack.SelectNodes(".//*"))
            {
                if (nodo.NodeType !=
                    XmlNodeType.Element)
                {
                    continue;
                }

                string nombreNodo =
                    nodo.Name.ToLowerInvariant();

                if (nombreNodo == "t" ||
                    nombreNodo == "frame" ||
                    nombreNodo == "transform")
                {
                    nodosFrame.Add(nodo);
                }
            }
        }

        // ========================================================
        // FRAMES
        // ========================================================

        for (
            int indiceFrame = 0;
            indiceFrame < nodosFrame.Count;
            indiceFrame++)
        {
            XmlNode nodoFrame =
                nodosFrame[indiceFrame];

            PvZReanimFrame frame =
                new PvZReanimFrame();

            // ====================================================
            // X
            // ====================================================

            float valor;

            if (TryObtenerFloat(
                nodoFrame,
                "x",
                out valor))
            {
                ultimoX = valor;
            }

            frame.x =
                ultimoX;

            // ====================================================
            // Y
            // ====================================================

            if (TryObtenerFloat(
                nodoFrame,
                "y",
                out valor))
            {
                ultimoY = valor;
            }

            frame.y =
                ultimoY;

            // ====================================================
            // SCALE X
            // ====================================================

            if (TryObtenerFloat(
                nodoFrame,
                "sx",
                out valor))
            {
                ultimoSX = valor;
            }

            frame.sx =
                ultimoSX;

            // ====================================================
            // SCALE Y
            // ====================================================

            if (TryObtenerFloat(
                nodoFrame,
                "sy",
                out valor))
            {
                ultimoSY = valor;
            }

            frame.sy =
                ultimoSY;

            // ====================================================
            // ROTACIÓN
            // ====================================================

            if (TryObtenerFloat(
                nodoFrame,
                "r",
                out valor))
            {
                ultimoF = valor;
            }
            else if (TryObtenerFloat(
                nodoFrame,
                "f",
                out valor))
            {
                ultimoF = valor;
            }

            frame.f =
                ultimoF;

            // ====================================================
            // IMAGEN
            // ====================================================

            string imagen =
                ObtenerValor(
                    nodoFrame,
                    "i");

            if (string.IsNullOrWhiteSpace(imagen))
            {
                imagen =
                    ObtenerValor(
                        nodoFrame,
                        "image");
            }

            if (string.IsNullOrWhiteSpace(imagen))
            {
                imagen =
                    ObtenerValor(
                        nodoFrame,
                        "img");
            }

            if (!string.IsNullOrWhiteSpace(imagen))
            {
                ultimaImagen =
                    imagen.Trim();
            }

            frame.image =
                ultimaImagen;

            // ====================================================
            // TRANSFORMACIÓN
            // ====================================================

            frame.tieneTransformacion =
                true;

            // ====================================================
            // GUARDAR
            // ====================================================

            track.frames.Add(
                frame);

            // ====================================================
            // DEBUG
            // ====================================================

            if (indiceTrack == 0 &&
                indiceFrame < 10)
            {
                Debug.Log(
                    "[PvZ REANIM PARSER] " +
                    "Track=" +
                    indiceTrack +
                    " | " +
                    track.name +
                    " | Frame=" +
                    indiceFrame +
                    " | X=" +
                    frame.x +
                    " | Y=" +
                    frame.y +
                    " | SX=" +
                    frame.sx +
                    " | SY=" +
                    frame.sy +
                    " | F=" +
                    frame.f +
                    " | IMG=" +
                    frame.image);
            }
        }

        Debug.Log(
            "[PvZ REANIM PARSER] Track " +
            indiceTrack +
            " '" +
            track.name +
            "' | Frames=" +
            track.frames.Count);

        return track;
    }

    // ============================================================
    // OBTENER VALOR DE ELEMENTO
    // ============================================================

    private static string ObtenerValor(
        XmlNode nodo,
        string nombre)
    {
        if (nodo == null)
        {
            return null;
        }

        // --------------------------------------------------------
        // Primero buscar como atributo.
        // --------------------------------------------------------

        string atributo =
            ObtenerAtributo(
                nodo,
                nombre);

        if (!string.IsNullOrWhiteSpace(atributo))
        {
            return atributo;
        }

        // --------------------------------------------------------
        // Buscar elemento hijo directo.
        // --------------------------------------------------------

        foreach (XmlNode hijo in nodo.ChildNodes)
        {
            if (hijo.NodeType !=
                XmlNodeType.Element)
            {
                continue;
            }

            if (string.Equals(
                hijo.Name,
                nombre,
                StringComparison.OrdinalIgnoreCase))
            {
                return hijo.InnerText;
            }
        }

        return null;
    }

    // ============================================================
    // FLOAT
    // ============================================================

    private static bool TryObtenerFloat(
        XmlNode nodo,
        string nombre,
        out float resultado)
    {
        resultado = 0f;

        string texto =
            ObtenerValor(
                nodo,
                nombre);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        texto =
            texto.Trim();

        // --------------------------------------------------------
        // Cultura invariante.
        // --------------------------------------------------------

        if (float.TryParse(
            texto,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out resultado))
        {
            return true;
        }

        // --------------------------------------------------------
        // Cultura actual.
        // --------------------------------------------------------

        if (float.TryParse(
            texto,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out resultado))
        {
            return true;
        }

        return false;
    }

    // ============================================================
    // ATRIBUTO
    // ============================================================

    private static string ObtenerAtributo(
        XmlNode nodo,
        string nombre)
    {
        if (nodo == null ||
            nodo.Attributes == null)
        {
            return null;
        }

        XmlAttribute atributo =
            nodo.Attributes[nombre];

        if (atributo != null)
        {
            return atributo.Value;
        }

        foreach (
            XmlAttribute attr
            in nodo.Attributes)
        {
            if (string.Equals(
                attr.Name,
                nombre,
                StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }
        }

        return null;
    }

    // ============================================================
    // LIMPIAR TEXTO
    // ============================================================

    private static string LimpiarTexto(
        string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        // BOM UTF-8.
        texto =
            texto.TrimStart(
                '\uFEFF',
                '\u200B',
                '\u0000');

        return texto.Trim();
    }

    // ============================================================
    // DECODIFICACIÓN
    // ============================================================

    private static string Decodificar(
        byte[] datos)
    {
        if (datos == null ||
            datos.Length == 0)
        {
            return string.Empty;
        }

        // --------------------------------------------------------
        // Detectar BOM UTF-8.
        // --------------------------------------------------------

        if (datos.Length >= 3 &&
            datos[0] == 0xEF &&
            datos[1] == 0xBB &&
            datos[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(
                datos,
                3,
                datos.Length - 3);
        }

        // --------------------------------------------------------
        // UTF-16 LE BOM.
        // --------------------------------------------------------

        if (datos.Length >= 2 &&
            datos[0] == 0xFF &&
            datos[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(
                datos,
                2,
                datos.Length - 2);
        }

        // --------------------------------------------------------
        // UTF-16 BE BOM.
        // --------------------------------------------------------

        if (datos.Length >= 2 &&
            datos[0] == 0xFE &&
            datos[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(
                datos,
                2,
                datos.Length - 2);
        }

        // --------------------------------------------------------
        // UTF-8 normal.
        // --------------------------------------------------------

        string utf8 =
            Encoding.UTF8.GetString(datos);

        if (utf8.IndexOf('<') >= 0)
        {
            return utf8;
        }

        // --------------------------------------------------------
        // UTF-16 LE.
        // --------------------------------------------------------

        if (datos.Length >= 2)
        {
            string unicode =
                Encoding.Unicode.GetString(datos);

            if (unicode.IndexOf('<') >= 0)
            {
                return unicode;
            }

            // ----------------------------------------------------
            // UTF-16 BE.
            // ----------------------------------------------------

            string bigEndian =
                Encoding.BigEndianUnicode.GetString(datos);

            if (bigEndian.IndexOf('<') >= 0)
            {
                return bigEndian;
            }
        }

        // --------------------------------------------------------
        // ASCII.
        // --------------------------------------------------------

        return Encoding.ASCII.GetString(
            datos);
    }
}