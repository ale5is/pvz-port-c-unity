using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using UnityEngine;

public static class PvZReanimParser
{
    private const float DEFAULT_SCALE = 1f;

    // ============================================================
    // BYTE[]
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
            return Parse(
                Decodificar(datos),
                nombre);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[PvZ REANIM] Error leyendo " +
                nombre +
                ":\n" +
                ex);

            return null;
        }
    }

    // ============================================================
    // STRING
    // ============================================================

    public static PvZReanimData Parse(
        string texto,
        string nombre = "")
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        try
        {
            texto =
                LimpiarTexto(texto);

            if (texto.Length == 0)
            {
                return null;
            }

            return ParseXml(
                texto,
                nombre);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "[PvZ REANIM] Error parser:\n" +
                ex);

            return null;
        }
    }

    // ============================================================
    // XML
    // ============================================================

    private static PvZReanimData ParseXml(
        string texto,
        string nombre)
    {
        XmlDocument documento =
            new XmlDocument();

        bool cargado = false;

        try
        {
            documento.LoadXml(texto);

            cargado = true;
        }
        catch
        {
            // Algunos REANIM pueden venir
            // sin elemento raíz.
        }

        if (!cargado)
        {
            try
            {
                documento =
                    new XmlDocument();

                documento.LoadXml(
                    "<reanim>" +
                    texto +
                    "</reanim>");

                cargado = true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PvZ REANIM] XML inválido:\n" +
                    ex);

                return null;
            }
        }

        PvZReanimData resultado =
            new PvZReanimData();

        // ========================================================
        // FPS
        // ========================================================

        float fps;

        if (TryObtenerFloat(
            documento.DocumentElement,
            "fps",
            out fps))
        {
            if (fps > 0f)
            {
                resultado.fps =
                    fps;
            }
        }

        // ========================================================
        // TRACKS
        // ========================================================

        XmlNodeList nodosTrack =
            documento.SelectNodes(
                "//track");

        if (
            nodosTrack == null ||
            nodosTrack.Count == 0)
        {
            nodosTrack =
                documento.SelectNodes(
                    "//Track");
        }

        if (nodosTrack != null)
        {
            for (
                int i = 0;
                i < nodosTrack.Count;
                i++)
            {
                PvZReanimTrack track =
                    ParseTrack(
                        nodosTrack[i],
                        i);

                if (track != null)
                {
                    resultado.tracks.Add(
                        track);
                }
            }
        }

        Debug.Log(
            "[PvZ REANIM] Parse terminado: " +
            nombre +
            " | FPS=" +
            resultado.fps +
            " | Tracks=" +
            resultado.tracks.Count);

        return resultado;
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
                "track_" +
                indiceTrack;
        }

        track.name =
            nombre.Trim();

        // ========================================================
        // VALORES HEREDADOS
        // ========================================================

        float ultimoX = 0f;
        float ultimoY = 0f;

        float ultimoKX = 0f;
        float ultimoKY = 0f;

        float ultimoSX =
            DEFAULT_SCALE;

        float ultimoSY =
            DEFAULT_SCALE;

        float ultimoAlpha = 1f;

        int ultimoImageFrame = 0;

        string ultimaImagen = null;

        // ========================================================
        // FRAMES
        // ========================================================

        List<XmlNode> nodosFrame =
            ObtenerNodosFrame(
                nodoTrack);

        for (
            int i = 0;
            i < nodosFrame.Count;
            i++)
        {
            XmlNode nodo =
                nodosFrame[i];

            PvZReanimFrame frame =
                new PvZReanimFrame();

            // ====================================================
            // IMPORTANTE:
            //
            // EL ÍNDICE DEL <t> ES EL FRAME TEMPORAL.
            //
            // f NO SE USA AQUÍ.
            // ====================================================

            frame.frameNumber =
                i;

            float valor;

            // ====================================================
            // X
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "x",
                out valor))
            {
                ultimoX =
                    valor;
            }

            frame.x =
                ultimoX;

            // ====================================================
            // Y
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "y",
                out valor))
            {
                ultimoY =
                    valor;
            }

            frame.y =
                ultimoY;

            // ====================================================
            // KX
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "kx",
                out valor))
            {
                ultimoKX =
                    valor;
            }

            frame.kx =
                ultimoKX;

            // ====================================================
            // KY
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "ky",
                out valor))
            {
                ultimoKY =
                    valor;
            }

            frame.ky =
                ultimoKY;

            // ====================================================
            // SX
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "sx",
                out valor))
            {
                ultimoSX =
                    valor;
            }

            frame.sx =
                ultimoSX;

            // ====================================================
            // SY
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "sy",
                out valor))
            {
                ultimoSY =
                    valor;
            }

            frame.sy =
                ultimoSY;

            // ====================================================
            // ALPHA
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "a",
                out valor))
            {
                ultimoAlpha =
                    valor;
            }

            frame.alpha =
                ultimoAlpha;

            // ====================================================
            // F
            //
            // ESTE ES EL FRAME/CEL DE LA IMAGEN.
            //
            // -1 = ocultar
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "f",
                out valor))
            {
                ultimoImageFrame =
                    Mathf.RoundToInt(
                        valor);
            }

            frame.imageFrame =
                ultimoImageFrame;

            // ====================================================
            // I
            // ====================================================

            string imagen =
                ObtenerValor(
                    nodo,
                    "i");

            if (
                !string.IsNullOrWhiteSpace(
                    imagen))
            {
                ultimaImagen =
                    imagen.Trim();
            }

            frame.image =
                ultimaImagen;

            frame.tieneTransformacion =
                true;

            track.frames.Add(
                frame);
        }

        Debug.Log(
            "[PvZ REANIM] Track " +
            indiceTrack +
            " '" +
            track.name +
            "' | Frames=" +
            track.frames.Count);

        return track;
    }

    // ============================================================
    // OBTENER <t>
    // ============================================================

    private static List<XmlNode> ObtenerNodosFrame(
        XmlNode nodoTrack)
    {
        List<XmlNode> resultado =
            new List<XmlNode>();

        XmlNodeList nodos =
            nodoTrack.SelectNodes(
                "./t");

        if (
            nodos != null &&
            nodos.Count > 0)
        {
            foreach (XmlNode nodo in nodos)
            {
                if (
                    nodo.NodeType ==
                    XmlNodeType.Element)
                {
                    resultado.Add(
                        nodo);
                }
            }

            return resultado;
        }

        nodos =
            nodoTrack.SelectNodes(
                "./frame");

        if (nodos != null)
        {
            foreach (XmlNode nodo in nodos)
            {
                if (
                    nodo.NodeType ==
                    XmlNodeType.Element)
                {
                    resultado.Add(
                        nodo);
                }
            }
        }

        return resultado;
    }

    // ============================================================
    // VALOR
    // ============================================================

    private static string ObtenerValor(
        XmlNode nodo,
        string nombre)
    {
        if (nodo == null)
        {
            return null;
        }

        string atributo =
            ObtenerAtributo(
                nodo,
                nombre);

        if (!string.IsNullOrWhiteSpace(
            atributo))
        {
            return atributo;
        }

        foreach (
            XmlNode hijo
            in nodo.ChildNodes)
        {
            if (
                hijo.NodeType !=
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
    // ATRIBUTO
    // ============================================================

    private static string ObtenerAtributo(
        XmlNode nodo,
        string nombre)
    {
        if (
            nodo == null ||
            nodo.Attributes == null)
        {
            return null;
        }

        foreach (
            XmlAttribute atributo
            in nodo.Attributes)
        {
            if (string.Equals(
                atributo.Name,
                nombre,
                StringComparison.OrdinalIgnoreCase))
            {
                return atributo.Value;
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

        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return false;
        }

        return float.TryParse(
            texto.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out resultado);
    }

    // ============================================================
    // LIMPIAR
    // ============================================================

    private static string LimpiarTexto(
        string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        return texto
            .TrimStart(
                '\uFEFF',
                '\u200B',
                '\u0000')
            .Trim();
    }

    // ============================================================
    // DECODIFICAR
    // ============================================================

    private static string Decodificar(
        byte[] datos)
    {
        if (
            datos == null ||
            datos.Length == 0)
        {
            return string.Empty;
        }

        // UTF-8 BOM.
        if (
            datos.Length >= 3 &&
            datos[0] == 0xEF &&
            datos[1] == 0xBB &&
            datos[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(
                datos,
                3,
                datos.Length - 3);
        }

        // UTF-16 LE.
        if (
            datos.Length >= 2 &&
            datos[0] == 0xFF &&
            datos[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(
                datos,
                2,
                datos.Length - 2);
        }

        // UTF-16 BE.
        if (
            datos.Length >= 2 &&
            datos[0] == 0xFE &&
            datos[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(
                datos,
                2,
                datos.Length - 2);
        }

        return Encoding.UTF8.GetString(
            datos);
    }
}