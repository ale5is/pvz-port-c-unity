using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEngine;

public class PvZReanimReader
{
    public class Reanim
    {
        public int fps;
        public List<Track> tracks = new List<Track>();
    }

    public class Track
    {
        public string nombre;
        public List<Frame> frames = new List<Frame>();
    }

    public class Frame
    {
        public int numero;

        public float x;
        public float y;

        public float sx = 1f;
        public float sy = 1f;

        public float rot;

        public float alpha = 1f;

        public string image;
        public string font;
        public string text;

        public bool visible = true;
    }

    public static Reanim Leer(byte[] datos)
    {
        if (datos == null || datos.Length == 0)
            throw new Exception("El REANIM está vacío.");

        string xml = System.Text.Encoding.UTF8.GetString(datos);

        XDocument documento = XDocument.Parse(xml);

        Reanim resultado = new Reanim();

        XElement fpsElement = documento.Root.Element("fps");

        if (fpsElement != null)
            int.TryParse(fpsElement.Value, out resultado.fps);

        foreach (XElement trackElement in documento.Root.Elements("track"))
        {
            Track track = new Track();

            XElement nameElement = trackElement.Element("name");

            if (nameElement != null)
                track.nombre = nameElement.Value;

            foreach (XElement frameElement in trackElement.Elements("t"))
            {
                Frame frame = LeerFrame(frameElement);

                if (frame != null)
                    track.frames.Add(frame);
            }

            resultado.tracks.Add(track);
        }

        return resultado;
    }

    private static Frame LeerFrame(XElement elemento)
    {
        Frame frame = new Frame();

        XElement f = elemento.Element("f");

        if (f == null)
            return null;

        if (!int.TryParse(f.Value, out frame.numero))
            frame.numero = -1;

        XElement x = elemento.Element("x");
        XElement y = elemento.Element("y");

        XElement sx = elemento.Element("sx");
        XElement sy = elemento.Element("sy");

        XElement rot = elemento.Element("r");
        XElement alpha = elemento.Element("a");

        if (x != null)
            float.TryParse(
                x.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.x);

        if (y != null)
            float.TryParse(
                y.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.y);

        if (sx != null)
            float.TryParse(
                sx.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.sx);

        if (sy != null)
            float.TryParse(
                sy.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.sy);

        if (rot != null)
            float.TryParse(
                rot.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.rot);

        if (alpha != null)
            float.TryParse(
                alpha.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out frame.alpha);

        XElement image = elemento.Element("i");

        if (image != null)
            frame.image = image.Value;

        XElement text = elemento.Element("text");

        if (text != null)
            frame.text = text.Value;

        XElement font = elemento.Element("font");

        if (font != null)
            frame.font = font.Value;

        XElement vis = elemento.Element("v");

        if (vis != null)
            frame.visible = vis.Value != "0";

        return frame;
    }
}