using System;
using System.Globalization;
using System.Text;
using System.Xml;

/// <summary>
/// Parser de archivos REANIM de Plants vs. Zombies.
/// Utiliza las clases PvZReanimData,
/// PvZReanimTrack y PvZReanimFrame
/// definidas en su propio archivo.
/// </summary>
public static class PvZReanimParser
{
    /// <summary>
    /// Analiza un REANIM directamente desde sus bytes.
    /// </summary>
    public static PvZReanimData Parse(byte[] datos)
    {
        if (datos == null || datos.Length == 0)
        {
            throw new ArgumentException(
                "Los datos REANIM están vacíos.");
        }

        string xml = Encoding.UTF8.GetString(datos);

        return Parse(xml);
    }

    /// <summary>
    /// Analiza un REANIM desde texto XML.
    /// </summary>
    public static PvZReanimData Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException(
                "El XML REANIM está vacío.");
        }

        xml = PrepararXml(xml);

        XmlDocument documento = new XmlDocument();

        documento.PreserveWhitespace = true;

        documento.LoadXml(xml);

        XmlElement raiz = documento.DocumentElement;

        if (raiz == null)
        {
            throw new XmlException(
                "No se encontró la raíz del REANIM.");
        }

        PvZReanimData resultado =
            new PvZReanimData();

        // =========================================================
        // FPS
        // =========================================================

        XmlNode fpsNode =
            raiz.SelectSingleNode("fps");

        if (fpsNode != null)
        {
            float fps;

            if (float.TryParse(
                fpsNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out fps))
            {
                if (fps > 0)
                {
                    resultado.fps = fps;
                }
            }
        }

        if (resultado.fps <= 0)
        {
            resultado.fps = 12f;
        }

        // =========================================================
        // TRACKS
        // =========================================================

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

            // -----------------------------------------------------
            // Nombre
            // -----------------------------------------------------

            XmlNode nameNode =
                trackNode.SelectSingleNode("name");

            if (nameNode != null)
            {
                track.name =
                    nameNode.InnerText.Trim();
            }
            else
            {
                track.name = string.Empty;
            }

            // -----------------------------------------------------
            // Frames
            // -----------------------------------------------------

            XmlNodeList frameNodes =
                trackNode.SelectNodes("t");

            if (frameNodes != null)
            {
                int indice = 0;

                foreach (XmlNode frameNode in frameNodes)
                {
                    PvZReanimFrame frame =
                        ParseFrame(
                            frameNode,
                            indice);

                    track.frames.Add(frame);

                    indice++;
                }
            }

            resultado.tracks.Add(track);
        }

        return resultado;
    }

    /// <summary>
    /// Analiza un frame individual.
    /// </summary>
    private static PvZReanimFrame ParseFrame(
        XmlNode node,
        int indice)
    {
        PvZReanimFrame frame =
            new PvZReanimFrame();

        frame.x = 0f;
        frame.y = 0f;
        frame.sx = 1f;
        frame.sy = 1f;
        frame.f = -1;
        frame.image = null;
        frame.tieneTransformacion = false;

        if (node == null)
        {
            return frame;
        }

        bool tieneDatos = false;

        // =========================================================
        // X
        // =========================================================

        XmlNode xNode =
            node.SelectSingleNode("x");

        if (xNode != null)
        {
            float valor;

            if (float.TryParse(
                xNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out valor))
            {
                frame.x = valor;
                tieneDatos = true;
            }
        }

        // =========================================================
        // Y
        // =========================================================

        XmlNode yNode =
            node.SelectSingleNode("y");

        if (yNode != null)
        {
            float valor;

            if (float.TryParse(
                yNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out valor))
            {
                frame.y = valor;
                tieneDatos = true;
            }
        }

        // =========================================================
        // SX
        // =========================================================

        XmlNode sxNode =
            node.SelectSingleNode("sx");

        if (sxNode != null)
        {
            float valor;

            if (float.TryParse(
                sxNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out valor))
            {
                frame.sx = valor;
                tieneDatos = true;
            }
        }

        // =========================================================
        // SY
        // =========================================================

        XmlNode syNode =
            node.SelectSingleNode("sy");

        if (syNode != null)
        {
            float valor;

            if (float.TryParse(
                syNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out valor))
            {
                frame.sy = valor;
                tieneDatos = true;
            }
        }

        // =========================================================
        // F
        // =========================================================

        XmlNode fNode =
            node.SelectSingleNode("f");

        if (fNode != null)
        {
            float valor;

            if (float.TryParse(
                fNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out valor))
            {
                frame.f = (int)valor;
                tieneDatos = true;
            }
        }

        // =========================================================
        // IMAGE
        // =========================================================

        XmlNode imageNode =
            node.SelectSingleNode("i");

        if (imageNode != null)
        {
            string nombre =
                imageNode.InnerText.Trim();

            if (!string.IsNullOrEmpty(nombre))
            {
                frame.image = nombre;
                tieneDatos = true;
            }
        }

        // =========================================================
        // ESTADO
        // =========================================================

        frame.tieneTransformacion = tieneDatos;

        return frame;
    }

    /// <summary>
    /// Prepara el XML de PvZ para XmlDocument.
    ///
    /// Los REANIM de PvZ utilizan varios elementos
    /// al mismo nivel:
    ///
    /// <fps>12</fps>
    /// <track>...</track>
    /// <track>...</track>
    ///
    /// XmlDocument necesita una única raíz.
    /// Por eso agregamos una raíz artificial:
    ///
    /// <reanim>
    ///     ...
    /// </reanim>
    /// </summary>
    private static string PrepararXml(string xml)
    {
        xml = xml.Trim();

        // =========================================================
        // Eliminar declaración XML
        // =========================================================

        if (xml.StartsWith(
            "<?xml",
            StringComparison.OrdinalIgnoreCase))
        {
            int finDeclaracion =
                xml.IndexOf(
                    "?>",
                    StringComparison.Ordinal);

            if (finDeclaracion >= 0)
            {
                xml =
                    xml.Substring(
                        finDeclaracion + 2);
            }
        }

        xml = xml.Trim();

        // =========================================================
        // Si ya tiene raíz <reanim>, no envolver otra vez.
        // =========================================================

        if (xml.StartsWith(
            "<reanim>",
            StringComparison.OrdinalIgnoreCase))
        {
            return xml;
        }

        // =========================================================
        // Agregar raíz artificial.
        // =========================================================

        return
            "<reanim>\n" +
            xml +
            "\n</reanim>";
    }
}