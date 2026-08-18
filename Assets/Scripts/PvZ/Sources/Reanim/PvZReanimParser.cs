using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using UnityEngine;

public static class PvZReanimParser
{
    private const float DEFAULT_SCALE = 1f;

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

            return ParseXmlFlexible(
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

    private static PvZReanimData ParseXmlFlexible(
        string texto,
        string nombre)
    {
        PvZReanimData resultado =
            new PvZReanimData();

        XmlDocument documento =
            new XmlDocument();

        bool correcto = false;

        try
        {
            documento.LoadXml(texto);
            correcto = true;
        }
        catch (XmlException)
        {
        }

        if (!correcto)
        {
            try
            {
                documento =
                    new XmlDocument();

                documento.LoadXml(
                    "<reanim>" +
                    texto +
                    "</reanim>");

                correcto = true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PvZ REANIM] XML inválido:\n" +
                    ex);

                return null;
            }
        }

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

        ParsearDocumento(
            documento,
            resultado);

        Debug.Log(
            "[PvZ REANIM] Parse terminado: " +
            nombre +
            " | FPS=" +
            resultado.fps +
            " | Tracks=" +
            resultado.tracks.Count);

        return resultado;
    }

    private static void ParsearDocumento(
        XmlDocument documento,
        PvZReanimData resultado)
    {
        XmlNodeList nodos =
            documento.SelectNodes(
                "//track");

        if (nodos == null ||
            nodos.Count == 0)
        {
            nodos =
                documento.SelectNodes(
                    "//Track");
        }

        if (nodos == null)
        {
            return;
        }

        int indice =
            0;

        foreach (XmlNode nodo in nodos)
        {
            PvZReanimTrack track =
                ParseTrack(
                    nodo,
                    indice);

            if (track != null)
            {
                resultado.tracks.Add(
                    track);
            }

            indice++;
        }
    }

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
                "track_" +
                indiceTrack;
        }

        track.name =
            nombre.Trim();

        // ========================================================
        // ESTADO HEREDADO
        // ========================================================

        float ultimoX =
            0f;

        float ultimoY =
            0f;

        float ultimoKX =
            0f;

        float ultimoKY =
            0f;

        float ultimoSX =
            DEFAULT_SCALE;

        float ultimoSY =
            DEFAULT_SCALE;

        float ultimaRotacion =
            0f;

        float ultimoAlpha =
            1f;

        string ultimaImagen =
            null;

        // ========================================================
        // OBTENER KEYFRAMES
        // ========================================================

        List<XmlNode> frames =
            ObtenerNodosFrame(
                nodoTrack);

        // ========================================================
        // LEER KEYFRAMES
        // ========================================================

        for (
            int i = 0;
            i < frames.Count;
            i++)
        {
            XmlNode nodo =
                frames[i];

            PvZReanimFrame frame =
                new PvZReanimFrame();

            // ====================================================
            // FRAME REAL
            // ====================================================

            float numeroFrame;

            if (TryObtenerFloat(
                nodo,
                "f",
                out numeroFrame))
            {
                frame.frameNumber =
                    Mathf.RoundToInt(
                        numeroFrame);
            }
            else
            {
                frame.frameNumber =
                    i;
            }

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
            // SCALE X
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
            // SCALE Y
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
            // ROTACIÓN
            // ====================================================

            if (TryObtenerFloat(
                nodo,
                "r",
                out valor))
            {
                ultimaRotacion =
                    valor;
            }
            else if (TryObtenerFloat(
                nodo,
                "rot",
                out valor))
            {
                ultimaRotacion =
                    valor;
            }

            frame.rotation =
                ultimaRotacion;

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
            // IMAGEN
            // ====================================================

            string imagen =
                ObtenerValor(
                    nodo,
                    "i");

            if (string.IsNullOrWhiteSpace(
                imagen))
            {
                imagen =
                    ObtenerValor(
                        nodo,
                        "image");
            }

            if (!string.IsNullOrWhiteSpace(
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

        // ========================================================
        // ORDENAR POR FRAME REAL
        // ========================================================

        track.frames.Sort(
            delegate (
                PvZReanimFrame a,
                PvZReanimFrame b)
            {
                return a.frameNumber.CompareTo(
                    b.frameNumber);
            });

        // ========================================================
        // ELIMINAR DUPLICADOS
        // ========================================================

        if (track.frames.Count > 1)
        {
            List<PvZReanimFrame> limpios =
                new List<PvZReanimFrame>();

            for (
                int i = 0;
                i < track.frames.Count;
                i++)
            {
                PvZReanimFrame actual =
                    track.frames[i];

                if (
                    limpios.Count > 0 &&
                    limpios[
                        limpios.Count - 1
                    ].frameNumber ==
                    actual.frameNumber)
                {
                    // Conservamos el último.
                    limpios[
                        limpios.Count - 1
                    ] =
                        actual;
                }
                else
                {
                    limpios.Add(
                        actual);
                }
            }

            track.frames =
                limpios;
        }

        Debug.Log(
            "[PvZ REANIM] Track " +
            indiceTrack +
            " '" +
            track.name +
            "' | Frames=" +
            track.frames.Count +
            " | Primer=" +
            ObtenerPrimerFrame(track) +
            " | Último=" +
            ObtenerUltimoFrame(track));

        return track;
    }

    // ============================================================
    // OBTENER NODOS DE FRAME
    // ============================================================

    private static List<XmlNode> ObtenerNodosFrame(
        XmlNode nodoTrack)
    {
        List<XmlNode> frames =
            new List<XmlNode>();

        XmlNodeList t =
            nodoTrack.SelectNodes(
                "./t");

        if (t != null)
        {
            foreach (XmlNode nodo in t)
            {
                if (nodo.NodeType ==
                    XmlNodeType.Element)
                {
                    frames.Add(
                        nodo);
                }
            }
        }

        if (frames.Count == 0)
        {
            XmlNodeList f =
                nodoTrack.SelectNodes(
                    "./frame");

            if (f != null)
            {
                foreach (XmlNode nodo in f)
                {
                    if (nodo.NodeType ==
                        XmlNodeType.Element)
                    {
                        frames.Add(
                            nodo);
                    }
                }
            }
        }

        return frames;
    }

    // ============================================================
    // PRIMER FRAME
    // ============================================================

    private static int ObtenerPrimerFrame(
        PvZReanimTrack track)
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return 0;
        }

        return
            track.frames[0].frameNumber;
    }

    // ============================================================
    // ÚLTIMO FRAME
    // ============================================================

    private static int ObtenerUltimoFrame(
        PvZReanimTrack track)
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return 0;
        }

        return
            track.frames[
                track.frames.Count - 1
            ].frameNumber;
    }

    // ============================================================
    // OBTENER VALOR
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
        resultado =
            0f;

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
    // LIMPIAR
    // ============================================================

    private static string LimpiarTexto(
        string texto)
    {
        if (string.IsNullOrEmpty(
            texto))
        {
            return string.Empty;
        }

        return texto.TrimStart(
            '\uFEFF',
            '\u200B',
            '\u0000').Trim();
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

        string utf8 =
            Encoding.UTF8.GetString(
                datos);

        if (utf8.IndexOf('<') >= 0)
        {
            return utf8;
        }

        string unicode =
            Encoding.Unicode.GetString(
                datos);

        if (unicode.IndexOf('<') >= 0)
        {
            return unicode;
        }

        string bigEndian =
            Encoding.BigEndianUnicode.GetString(
                datos);

        if (bigEndian.IndexOf('<') >= 0)
        {
            return bigEndian;
        }

        return Encoding.ASCII.GetString(
            datos);
    }
}