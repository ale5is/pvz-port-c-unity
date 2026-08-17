using System;
using System.Collections.Generic;

/// <summary>
/// Datos completos de una animación REANIM.
/// </summary>
[Serializable]
public class PvZReanimData
{
    public float fps = 12f;

    public List<PvZReanimTrack> tracks =
        new List<PvZReanimTrack>();
}

/// <summary>
/// Una pieza del REANIM.
/// </summary>
[Serializable]
public class PvZReanimTrack
{
    public string name = "";

    public List<PvZReanimFrame> frames =
        new List<PvZReanimFrame>();
}

/// <summary>
/// Estado de una pieza en un frame.
/// </summary>
[Serializable]
public class PvZReanimFrame
{
    public float x;
    public float y;

    public float sx = 1f;
    public float sy = 1f;

    public int f = -1;

    public string image;

    public bool tieneTransformacion;

    public bool TieneImagen
    {
        get
        {
            return !string.IsNullOrWhiteSpace(image);
        }
    }

    public PvZReanimFrame Copiar()
    {
        return new PvZReanimFrame
        {
            x = x,
            y = y,
            sx = sx,
            sy = sy,
            f = f,
            image = image,
            tieneTransformacion = tieneTransformacion
        };
    }

    public override string ToString()
    {
        return
            "X=" + x +
            " Y=" + y +
            " SX=" + sx +
            " SY=" + sy +
            " F=" + f +
            " IMAGE=" + image;
    }
}