using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using UnityEngine;

public static class PvZReanimParser
{
    public static PvZReanimData Parse(
        byte[] datos,
        string nombre = "REANIM")
    {
        if (datos == null || datos.Length == 0)
            return null;

        try
        {
            string texto =
                Encoding.UTF8.GetString(datos);

            return ParseTexto(
                texto,
                nombre);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimParser] Error parseando " +
                nombre +
                ":\n" +
                e);

            return null;
        }
    }

    public static bool TryParse(
        byte[] datos,
        out PvZReanimData resultado,
        string nombre = "REANIM")
    {
        resultado =
            Parse(
                datos,
                nombre);

        return resultado != null;
    }

    private static PvZReanimData ParseTexto(
        string texto,
        string nombre)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        texto =
            LimpiarTexto(
                texto);

        if (string.IsNullOrWhiteSpace(texto))
            return null;

        /*
         * Los REANIM de PvZ no son XML convencional.
         *
         * Tienen múltiples elementos raíz:
         *
         * <fps>12</fps>
         * <track>...</track>
         * <track>...</track>
         *
         * XML exige un único elemento raíz.
         *
         * Por eso creamos uno temporal:
         *
         * <reanim>
         *     ...
         * </reanim>
         */

        string xml =
            "<reanim>\n" +
            texto +
            "\n</reanim>";

        XmlDocument documento =
            new XmlDocument();

        documento.PreserveWhitespace =
            true;

        documento.LoadXml(
            xml);

        PvZReanimData resultado =
            new PvZReanimData();

        resultado.fps =
            LeerFPS(
                documento);

        if (resultado.fps <= 0f)
            resultado.fps = 12f;

        resultado.tracks =
            new List<PvZReanimTrack>();

        XmlNodeList nodosTrack =
            documento.SelectNodes(
                "/reanim/track");

        if (nodosTrack == null)
        {
            Debug.LogWarning(
                "[PvZ ReanimParser] No se encontraron tracks en " +
                nombre);

            return resultado;
        }

        for (
            int i = 0;
            i < nodosTrack.Count;
            i++)
        {
            XmlNode nodoTrack =
                nodosTrack[i];

            if (nodoTrack == null)
                continue;

            PvZReanimTrack track =
                ParseTrack(
                    nodoTrack,
                    i);

            if (track == null)
                continue;

            resultado.tracks.Add(
                track);
        }

        Debug.Log(
            "[PvZ ReanimParser] " +
            nombre +
            " | FPS=" +
            resultado.fps +
            " | Tracks=" +
            resultado.tracks.Count);

        return resultado;
    }

    // ============================================================
    // LIMPIEZA
    // ============================================================

    private static string LimpiarTexto(
        string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        texto =
            texto.TrimStart(
                '\uFEFF',
                '\u0000',
                ' ',
                '\t',
                '\r',
                '\n');

        /*
         * Algunos dumps pueden traer bytes nulos
         * antes del XML.
         */

        texto =
            texto.Replace(
                "\u0000",
                "");

        return texto.Trim();
    }

    // ============================================================
    // FPS
    // ============================================================

    private static float LeerFPS(
        XmlDocument documento)
    {
        XmlNode nodo =
            documento.SelectSingleNode(
                "/reanim/fps");

        if (nodo == null)
            return 12f;

        return ParseFloat(
            nodo.InnerText,
            12f);
    }

    // ============================================================
    // TRACK
    // ============================================================

    private static PvZReanimTrack ParseTrack(
        XmlNode nodoTrack,
        int indice)
    {
        PvZReanimTrack track =
            new PvZReanimTrack();

        XmlNode nodoNombre =
            nodoTrack.SelectSingleNode(
                "name");

        if (nodoNombre != null)
        {
            track.name =
                nodoNombre.InnerText.Trim();
        }

        if (string.IsNullOrWhiteSpace(
            track.name))
        {
            track.name =
                "Track_" +
                indice;
        }

        track.frames =
            new List<PvZReanimFrame>();

        XmlNodeList nodosFrame =
            nodoTrack.SelectNodes(
                "t");

        if (nodosFrame == null ||
            nodosFrame.Count == 0)
        {
            return track;
        }

        /*
         * ========================================================
         * ESTADO ACUMULADO
         * ========================================================
         *
         * REANIM no repite todos los valores en cada <t>.
         *
         * Ejemplo:
         *
         * Frame 4:
         *
         * <t>
         *     <x>27.7</x>
         *     <y>53</y>
         *     <sx>0.555</sx>
         *     <sy>0.555</sy>
         *     <f>0</f>
         * </t>
         *
         * Frame 5:
         *
         * <t>
         *     <y>53.3</y>
         *     <sx>0.561</sx>
         *     <sy>0.543</sy>
         * </t>
         *
         * Frame 5 conserva:
         *
         * x = 27.7
         *
         * porque x no aparece.
         *
         * Lo mismo ocurre con:
         *
         * y
         * sx
         * sy
         * kx
         * ky
         * alpha
         * f
         * i
         *
         * Por eso mantenemos un estado anterior.
         */

        PvZReanimFrame estado =
            CrearFrameInicial();

        for (
            int i = 0;
            i < nodosFrame.Count;
            i++)
        {
            XmlNode nodoFrame =
                nodosFrame[i];

            if (nodoFrame == null)
                continue;

            PvZReanimFrame frame =
                ResolverFrame(
                    nodoFrame,
                    i,
                    estado);

            track.frames.Add(
                frame);

            estado =
                frame;
        }

        return track;
    }

    // ============================================================
    // FRAME INICIAL
    // ============================================================

    private static PvZReanimFrame CrearFrameInicial()
    {
        PvZReanimFrame frame =
            new PvZReanimFrame();

        frame.frameNumber = 0;

        frame.x = 0f;
        frame.y = 0f;

        frame.kx = 0f;
        frame.ky = 0f;

        frame.sx = 1f;
        frame.sy = 1f;

        frame.alpha = 1f;

        /*
         * -1 significa invisible.
         *
         * Esto es importante porque muchos REANIM
         * comienzan con:
         *
         * <t><f>-1</f></t>
         */

        frame.imageFrame = -1;

        frame.image = null;

        frame.tieneTransformacion = false;

        return frame;
    }

    // ============================================================
    // RESOLVER FRAME
    // ============================================================

    private static PvZReanimFrame ResolverFrame(
        XmlNode nodo,
        int indice,
        PvZReanimFrame anterior)
    {
        PvZReanimFrame frame =
            anterior.Copiar();

        frame.frameNumber =
            indice;

        bool tieneTransformacion =
            false;

        // --------------------------------------------------------
        // X
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "x"))
        {
            frame.x =
                LeerFloat(
                    nodo,
                    "x",
                    frame.x);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // Y
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "y"))
        {
            frame.y =
                LeerFloat(
                    nodo,
                    "y",
                    frame.y);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // SCALE X
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "sx"))
        {
            frame.sx =
                LeerFloat(
                    nodo,
                    "sx",
                    frame.sx);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // SCALE Y
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "sy"))
        {
            frame.sy =
                LeerFloat(
                    nodo,
                    "sy",
                    frame.sy);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // SKEW X
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "kx"))
        {
            frame.kx =
                LeerFloat(
                    nodo,
                    "kx",
                    frame.kx);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // SKEW Y
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "ky"))
        {
            frame.ky =
                LeerFloat(
                    nodo,
                    "ky",
                    frame.ky);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // ALPHA
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "a"))
        {
            frame.alpha =
                LeerFloat(
                    nodo,
                    "a",
                    frame.alpha);

            tieneTransformacion = true;
        }

        // --------------------------------------------------------
        // IMAGE FRAME
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "f"))
        {
            frame.imageFrame =
                LeerInt(
                    nodo,
                    "f",
                    frame.imageFrame);
        }

        // --------------------------------------------------------
        // IMAGE
        // --------------------------------------------------------

        if (TieneNodo(
            nodo,
            "i"))
        {
            XmlNode nodoImagen =
                nodo.SelectSingleNode(
                    "i");

            if (nodoImagen != null)
            {
                string imagen =
                    nodoImagen.InnerText.Trim();

                if (!string.IsNullOrWhiteSpace(
                    imagen))
                {
                    frame.image =
                        imagen;
                }
            }
        }

        frame.tieneTransformacion =
            tieneTransformacion ||
            anterior.tieneTransformacion;

        return frame;
    }

    // ============================================================
    // TIENE NODO
    // ============================================================

    private static bool TieneNodo(
        XmlNode nodo,
        string nombre)
    {
        if (nodo == null)
            return false;

        XmlNode hijo =
            nodo.SelectSingleNode(
                nombre);

        if (hijo == null)
            return false;

        /*
         * Un nodo vacío:
         *
         * <x></x>
         *
         * no debe destruir el valor anterior.
         */

        string texto =
            hijo.InnerText;

        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return false;
        }

        return true;
    }

    // ============================================================
    // FLOAT
    // ============================================================

    private static float LeerFloat(
        XmlNode nodo,
        string nombre,
        float valorDefault)
    {
        XmlNode hijo =
            nodo.SelectSingleNode(
                nombre);

        if (hijo == null)
            return valorDefault;

        string texto =
            hijo.InnerText.Trim();

        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return valorDefault;
        }

        return ParseFloat(
            texto,
            valorDefault);
    }

    private static float ParseFloat(
        string texto,
        float valorDefault)
    {
        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return valorDefault;
        }

        float valor;

        if (float.TryParse(
            texto,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out valor))
        {
            return valor;
        }

        /*
         * Algunos archivos pueden usar coma decimal.
         * Lo intentamos como segunda posibilidad.
         */

        if (float.TryParse(
            texto,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out valor))
        {
            return valor;
        }

        return valorDefault;
    }

    // ============================================================
    // INT
    // ============================================================

    private static int LeerInt(
        XmlNode nodo,
        string nombre,
        int valorDefault)
    {
        XmlNode hijo =
            nodo.SelectSingleNode(
                nombre);

        if (hijo == null)
            return valorDefault;

        string texto =
            hijo.InnerText.Trim();

        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return valorDefault;
        }

        int valor;

        if (int.TryParse(
            texto,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out valor))
        {
            return valor;
        }

        return valorDefault;
    }
}