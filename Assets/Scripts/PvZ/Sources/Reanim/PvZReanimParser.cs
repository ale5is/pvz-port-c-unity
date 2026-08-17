using System;
using System.Globalization;
using System.Text;
using System.Xml;

/// <summary>
/// Parser de archivos REANIM de Plants vs. Zombies.
///
/// Convierte el XML original de PvZ en:
///
/// PvZReanimData
///     -> PvZReanimTrack
///         -> PvZReanimFrame
///
/// Los frames son acumulativos:
/// si un <t> no contiene un valor, conserva
/// el valor del frame anterior.
/// </summary>
public static class PvZReanimParser
{
    // ============================================================
    // PARSEAR DESDE BYTES
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
    // PARSEAR DESDE XML
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
                "No se encontró la raíz del REANIM.");
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
            float fps;

            if (float.TryParse(
                fpsNode.InnerText.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out fps))
            {
                if (fps > 0f)
                {
                    resultado.fps = fps;
                }
            }
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

            // ----------------------------------------------------
            // NOMBRE
            // ----------------------------------------------------

            XmlNode nameNode =
                trackNode.SelectSingleNode("name");

            if (nameNode != null)
            {
                track.name =
                    nameNode.InnerText.Trim();
            }
            else
            {
                track.name =
                    string.Empty;
            }

            // ----------------------------------------------------
            // FRAMES
            // ----------------------------------------------------

            XmlNodeList frameNodes =
                trackNode.SelectNodes("t");

            if (frameNodes != null)
            {
                PvZReanimFrame frameAnterior =
                    null;

                int indice = 0;

                foreach (XmlNode frameNode in frameNodes)
                {
                    PvZReanimFrame frame =
                        ParseFrame(
                            frameNode,
                            indice,
                            frameAnterior);

                    track.frames.Add(frame);

                    frameAnterior = frame;

                    indice++;
                }
            }

            resultado.tracks.Add(track);
        }

        return resultado;
    }

    // ============================================================
    // PARSEAR FRAME
    // ============================================================

    private static PvZReanimFrame ParseFrame(
        XmlNode node,
        int indice,
        PvZReanimFrame anterior)
    {
        PvZReanimFrame frame;

        // ========================================================
        // HERENCIA
        // ========================================================

        if (anterior != null)
        {
            frame =
                anterior.Copiar();
        }
        else
        {
            frame =
                new PvZReanimFrame();

            frame.x = 0f;
            frame.y = 0f;

            frame.sx = 1f;
            frame.sy = 1f;

            frame.f = -1;

            frame.image = null;

            frame.tieneTransformacion =
                false;
        }

        if (node == null)
        {
            return frame;
        }

        // ========================================================
        // TRANSFORMACIÓN DEL FRAME ACTUAL
        // ========================================================

        bool tieneTransformacionActual =
            false;

        // --------------------------------------------------------
        // X
        // --------------------------------------------------------

        XmlNode xNode =
            node.SelectSingleNode("x");

        if (xNode != null)
        {
            float valor;

            if (TryParseFloat(
                xNode.InnerText,
                out valor))
            {
                frame.x = valor;

                tieneTransformacionActual =
                    true;
            }
        }

        // --------------------------------------------------------
        // Y
        // --------------------------------------------------------

        XmlNode yNode =
            node.SelectSingleNode("y");

        if (yNode != null)
        {
            float valor;

            if (TryParseFloat(
                yNode.InnerText,
                out valor))
            {
                frame.y = valor;

                tieneTransformacionActual =
                    true;
            }
        }

        // --------------------------------------------------------
        // SX
        // --------------------------------------------------------

        XmlNode sxNode =
            node.SelectSingleNode("sx");

        if (sxNode != null)
        {
            float valor;

            if (TryParseFloat(
                sxNode.InnerText,
                out valor))
            {
                frame.sx = valor;

                tieneTransformacionActual =
                    true;
            }
        }

        // --------------------------------------------------------
        // SY
        // --------------------------------------------------------

        XmlNode syNode =
            node.SelectSingleNode("sy");

        if (syNode != null)
        {
            float valor;

            if (TryParseFloat(
                syNode.InnerText,
                out valor))
            {
                frame.sy = valor;

                tieneTransformacionActual =
                    true;
            }
        }

        // --------------------------------------------------------
        // F
        // --------------------------------------------------------

        XmlNode fNode =
            node.SelectSingleNode("f");

        if (fNode != null)
        {
            float valor;

            if (TryParseFloat(
                fNode.InnerText,
                out valor))
            {
                frame.f =
                    MathfRoundToInt(valor);

                tieneTransformacionActual =
                    true;
            }
        }

        // ========================================================
        // IMAGEN
        // ========================================================

        XmlNode imageNode =
            node.SelectSingleNode("i");

        if (imageNode != null)
        {
            string nombre =
                imageNode.InnerText.Trim();

            if (!string.IsNullOrEmpty(nombre))
            {
                frame.image =
                    nombre;
            }
        }

        // ========================================================
        // ESTADO
        // ========================================================

        // Si este frame tiene transformación explícita,
        // marcamos el frame como transformado.
        //
        // Si no tiene transformación pero heredó una del frame
        // anterior, conservamos el estado anterior.

        if (tieneTransformacionActual)
        {
            frame.tieneTransformacion =
                true;
        }

        return frame;
    }

    // ============================================================
    // PARSEAR FLOAT
    // ============================================================

    private static bool TryParseFloat(
        string texto,
        out float valor)
    {
        valor = 0f;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        return float.TryParse(
            texto.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out valor);
    }

    // ============================================================
    // REDONDEAR A INT
    // ============================================================

    private static int MathfRoundToInt(
        float valor)
    {
        if (valor >= 0f)
        {
            return (int)Math.Floor(
                valor + 0.5f);
        }

        return (int)Math.Ceiling(
            valor - 0.5f);
    }

    // ============================================================
    // PREPARAR XML
    // ============================================================

    /// <summary>
    /// Prepara el XML de PvZ para XmlDocument.
    ///
    /// Algunos REANIM tienen:
    ///
    /// <fps>12</fps>
    /// <track>...</track>
    /// <track>...</track>
    ///
    /// Por eso agregamos una raíz artificial.
    /// </summary>
    private static string PrepararXml(
        string xml)
    {
        xml =
            xml.Trim();

        // ========================================================
        // ELIMINAR DECLARACIÓN XML
        // ========================================================

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

        xml =
            xml.Trim();

        // ========================================================
        // SI YA TIENE RAÍZ REANIM
        // ========================================================

        if (xml.StartsWith(
            "<reanim>",
            StringComparison.OrdinalIgnoreCase))
        {
            return xml;
        }

        // ========================================================
        // RAÍZ ARTIFICIAL
        // ========================================================

        return
            "<reanim>\n" +
            xml +
            "\n</reanim>";
    }
}