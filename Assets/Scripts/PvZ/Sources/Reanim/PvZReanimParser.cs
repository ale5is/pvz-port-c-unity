using System;
using System.Collections.Generic;

public sealed class PvZReanimParser
{
    public sealed class ReanimData
    {
        public string Nombre;
        public float Duracion;
        public readonly List<Track> Tracks = new List<Track>();
    }

    public sealed class Track
    {
        public string Nombre;
        public readonly List<TransformFrame> Frames =
            new List<TransformFrame>();
    }

    public sealed class TransformFrame
    {
        public int Frame;
        public float X;
        public float Y;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float Rotacion;
        public float Alpha = 1f;
    }

    public static ReanimData Parse(
        byte[] datos,
        string nombre = "REANIM")
    {
        if (datos == null || datos.Length == 0)
            return null;

        ReanimData resultado = new ReanimData
        {
            Nombre = nombre,
            Duracion = 0f
        };

        return resultado;
    }

    public static bool TryParse(
        byte[] datos,
        out ReanimData resultado,
        string nombre = "REANIM")
    {
        resultado = Parse(datos, nombre);
        return resultado != null;
    }
}