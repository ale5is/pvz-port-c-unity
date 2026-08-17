using System;
using System.Globalization;
using System.Text;
using System.Xml;

public static class PvZReanimParser
{
    // ============================================================
    // BYTE[]
    // ============================================================

    public static PvZReanimData Parse(byte[] datos)
    {
        if (datos == null || datos.Length == 0)
        {
            throw new ArgumentException(
                "Los datos REANIM están vacíos.");
        }

        string xml =
            Encoding.UTF8.GetString(datos);

        return Parse(xml);
    }

    // ============================================================
    // STRING
    // ============================================================

    public static PvZReanimData Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException(
                "El XML REANIM está vacío.");
        }

        xml = PrepararXml(xml);

        XmlDocument documento =
            new XmlDocument();

        documento.PreserveWhitespace = true;

        documento.LoadXml(xml);

        XmlElement raiz =
            documento.DocumentElement;

        if (raiz == null)
        {
            throw new XmlException(
                "No existe raíz REANIM.");
        }

        PvZReanimData resultado =
            new PvZReanimData();

        // ========================================================
        // FPS
        // ========================================================

        XmlNode fpsNode =
            raiz.SelectSingleNode("fps");

        if (fpsNode != null)
        {
            resultado.fps =
                LeerFloat(
                    fpsNode.InnerText,
                    12f);
        }

        if (resultado.fps <= 0f)
        {
            resultado.fps = 12f;
        }

        // ========================================================
        // TRACKS
        // ========================================================

        XmlNodeList tracks =
            raiz.SelectNodes("track");

        if (tracks == null)
        {
            return resultado;
        }

        foreach (XmlNode trackNode in tracks)
        {
            if (trackNode == null)
            {
                continue;
            }

            PvZReanimTrack track =
                new PvZReanimTrack();

            XmlNode nameNode =
                trackNode.SelectSingleNode("name");

            if (nameNode != null)
            {
                track.name =
                    nameNode.InnerText.Trim();
            }

            // ====================================================
            // FRAMES
            // ====================================================

            XmlNodeList frameNodes =
                trackNode.SelectNodes("t");

            PvZReanimFrame estadoAnterior =
                new PvZReanimFrame();

            estadoAnterior.x = 0f;
            estadoAnterior.y = 0f;
            estadoAnterior.sx = 1f;
            estadoAnterior.sy = 1f;
            estadoAnterior.f = -1;
            estadoAnterior.image = null;

            if (frameNodes != null)
            {
                int indice = 0;

                foreach (XmlNode frameNode in frameNodes)
                {
                    PvZReanimFrame frame =
                        ParseFrame(
                            frameNode,
                            indice,
                            estadoAnterior);

                    track.frames.Add(frame);

                    estadoAnterior =
                        frame.Copiar();

                    indice++;
                }
            }

            resultado.tracks.Add(track);
        }

        return resultado;
    }

    // ============================================================
    // FRAME
    // ============================================================

    private static PvZReanimFrame ParseFrame(
        XmlNode node,
        int indice,
        PvZReanimFrame anterior)
    {
        PvZReanimFrame frame =
            anterior != null
                ? anterior.Copiar()
                : new PvZReanimFrame();

        if (frame.sx == 0f)
        {
            frame.sx = 1f;
        }

        if (frame.sy == 0f)
        {
            frame.sy = 1f;
        }

        frame.tieneTransformacion = false;

        if (node == null)
        {
            return frame;
        }

        // ========================================================
        // X
        // ========================================================

        XmlNode nodo =
            node.SelectSingleNode("x");

        if (nodo != null)
        {
            frame.x =
                LeerFloat(
                    nodo.InnerText,
                    frame.x);

            frame.tieneTransformacion = true;
        }

        // ========================================================
        // Y
        // ========================================================

        nodo =
            node.SelectSingleNode("y");

        if (nodo != null)
        {
            frame.y =
                LeerFloat(
                    nodo.InnerText,
                    frame.y);

            frame.tieneTransformacion = true;
        }

        // ========================================================
        // SX
        // ========================================================

        nodo =
            node.SelectSingleNode("sx");

        if (nodo != null)
        {
            frame.sx =
                LeerFloat(
                    nodo.InnerText,
                    frame.sx);

            if (Math.Abs(frame.sx) < 0.00001f)
            {
                frame.sx = 1f;
            }

            frame.tieneTransformacion = true;
        }

        // ========================================================
        // SY
        // ========================================================

        nodo =
            node.SelectSingleNode("sy");

        if (nodo != null)
        {
            frame.sy =
                LeerFloat(
                    nodo.InnerText,
                    frame.sy);

            if (Math.Abs(frame.sy) < 0.00001f)
            {
                frame.sy = 1f;
            }

            frame.tieneTransformacion = true;
        }

        // ========================================================
        // F
        // ========================================================

        nodo =
            node.SelectSingleNode("f");

        if (nodo != null)
        {
            float valor =
                LeerFloat(
                    nodo.InnerText,
                    frame.f);

            frame.f =
                MathfRoundToInt(valor);

            frame.tieneTransformacion = true;
        }

        // ========================================================
        // IMAGE
        // ========================================================

        nodo =
            node.SelectSingleNode("i");

        if (nodo != null)
        {
            string imagen =
                nodo.InnerText.Trim();

            if (!string.IsNullOrWhiteSpace(imagen))
            {
                frame.image =
                    imagen;
            }
        }

        return frame;
    }

    // ============================================================
    // FLOAT
    // ============================================================

    private static float LeerFloat(
        string texto,
        float valorPorDefecto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return valorPorDefecto;
        }

        float valor;

        if (float.TryParse(
            texto.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out valor))
        {
            return valor;
        }

        return valorPorDefecto;
    }

    // ============================================================
    // INT
    // ============================================================

    private static int MathfRoundToInt(float valor)
    {
        return (int)Math.Round(
            valor,
            MidpointRounding.AwayFromZero);
    }

    // ============================================================
    // PREPARAR XML
    // ============================================================

    private static string PrepararXml(
        string xml)
    {
        xml =
            xml.Trim();

        // --------------------------------------------------------
        // Declaración XML
        // --------------------------------------------------------

        if (xml.StartsWith(
            "<?xml",
            StringComparison.OrdinalIgnoreCase))
        {
            int fin =
                xml.IndexOf(
                    "?>",
                    StringComparison.Ordinal);

            if (fin >= 0)
            {
                xml =
                    xml.Substring(
                        fin + 2);
            }
        }

        xml =
            xml.Trim();

        // --------------------------------------------------------
        // Ya tiene raíz
        // --------------------------------------------------------

        if (xml.StartsWith(
            "<reanim",
            StringComparison.OrdinalIgnoreCase))
        {
            return xml;
        }

        // --------------------------------------------------------
        // Crear raíz artificial
        // --------------------------------------------------------

        return
            "<reanim>\n" +
            xml +
            "\n</reanim>";
    }
}