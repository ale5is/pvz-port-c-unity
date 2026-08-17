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
/// Una pieza individual del REANIM.
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
    // Posición REANIM.
    public float x;
    public float y;

    // Escala REANIM.
    public float sx = 1f;
    public float sy = 1f;

    // Rotación REANIM.
    // Se conserva como float para no perder precisión.
    public float f;

    // Imagen utilizada por este frame.
    public string image;

    // Indica si el frame contenía alguna
    // transformación explícita.
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