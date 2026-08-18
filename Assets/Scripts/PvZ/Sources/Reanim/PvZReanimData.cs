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

    // IMPORTANTE:
    // El índice de esta lista ES el frame de animación.
    // El campo f NO es el frame de animación.
    public List<PvZReanimFrame> frames =
        new List<PvZReanimFrame>();
}

[Serializable]
public class PvZReanimFrame
{
    // Frame real dentro del track.
    public int frameNumber;

    // Transformación.
    public float x;
    public float y;

    // En Resodded:
    // kx = SkewX
    // ky = SkewY
    public float kx;
    public float ky;

    public float sx = 1f;
    public float sy = 1f;

    // Alpha.
    public float alpha = 1f;

    // ============================================================
    // IMPORTANTE
    // ============================================================
    //
    // f NO representa el frame temporal.
    //
    // f representa el CEL/FRAME de la imagen.
    //
    // f = -1 -> ocultar track
    // f >= 0 -> mostrar track
    //
    public int imageFrame = 0;

    // Imagen.
    public string image;

    // ============================================================
    // COMPATIBILIDAD
    // ============================================================

    public bool tieneTransformacion;

    public bool TieneImagen
    {
        get
        {
            return !string.IsNullOrWhiteSpace(image);
        }
    }

    public bool Visible
    {
        get
        {
            return imageFrame >= 0;
        }
    }

    public PvZReanimFrame Copiar()
    {
        return new PvZReanimFrame
        {
            frameNumber = frameNumber,
            x = x,
            y = y,
            kx = kx,
            ky = ky,
            sx = sx,
            sy = sy,
            alpha = alpha,
            imageFrame = imageFrame,
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
            " KX=" + kx +
            " KY=" + ky +
            " SX=" + sx +
            " SY=" + sy +
            " A=" + alpha +
            " F=" + imageFrame +
            " IMAGE=" + image;
    }
}