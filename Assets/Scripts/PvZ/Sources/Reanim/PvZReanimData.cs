using System;
using System.Collections.Generic;

[Serializable]
public class PvZReanimData
{
    public float fps = 12f;

    public List<PvZReanimTrack> tracks =
        new List<PvZReanimTrack>();
}

[Serializable]
public class PvZReanimTrack
{
    public string name = "";

    public List<PvZReanimFrame> frames =
        new List<PvZReanimFrame>();
}

[Serializable]
public class PvZReanimFrame
{
    // Frame real dentro del REANIM.
    public int frameNumber;

    // Transformación.
    public float x;
    public float y;

    public float sx = 1f;
    public float sy = 1f;

    // Rotación.
    public float rotation;

    // Alpha.
    public float alpha = 1f;

    // Imagen.
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
            frameNumber = frameNumber,
            x = x,
            y = y,
            sx = sx,
            sy = sy,
            rotation = rotation,
            alpha = alpha,
            image = image,
            tieneTransformacion = tieneTransformacion
        };
    }

    public override string ToString()
    {
        return
            "Frame=" + frameNumber +
            " X=" + x +
            " Y=" + y +
            " SX=" + sx +
            " SY=" + sy +
            " R=" + rotation +
            " A=" + alpha +
            " IMAGE=" + image;
    }
}