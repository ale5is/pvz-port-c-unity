using System;
using System.Collections.Generic;

[Serializable]
public class PvZReanimData
{
    // FPS de reproducción de la REANIM.
    public float fps = 12f;

    // Todos los tracks/capas de la REANIM.
    public List<PvZReanimTrack> tracks =
        new List<PvZReanimTrack>();
}

[Serializable]
public class PvZReanimTrack
{
    // Nombre del track.
    public string name = "";

    // Keyframes del track.
    public List<PvZReanimFrame> frames =
        new List<PvZReanimFrame>();
}

[Serializable]
public class PvZReanimFrame
{
    // ============================================================
    // FRAME
    // ============================================================

    // Número REAL del frame dentro de la REANIM.
    //
    // Ejemplo:
    // 0
    // 3
    // 7
    // 12
    //
    // No necesariamente coincide con el índice de la lista.
    public int frameNumber;

    // ============================================================
    // POSICIÓN
    // ============================================================

    public float x;

    public float y;

    // ============================================================
    // SKEW / SHEAR
    // ============================================================
    //
    // Estos valores existen en ReanimatorTransform de Resodded.
    //
    // kx = skew horizontal
    // ky = skew vertical
    //
    // Todavía no se aplican directamente al SpriteRenderer,
    // pero ya los conservamos desde el parser para no perder
    // información del REANIM.
    //

    public float kx;

    public float ky;

    // ============================================================
    // ESCALA
    // ============================================================

    public float sx = 1f;

    public float sy = 1f;

    // ============================================================
    // ROTACIÓN
    // ============================================================

    public float rotation;

    // ============================================================
    // ALPHA
    // ============================================================

    public float alpha = 1f;

    // ============================================================
    // IMAGEN
    // ============================================================

    public string image;

    // ============================================================
    // ESTADO
    // ============================================================

    public bool tieneTransformacion;

    // ============================================================
    // PROPIEDADES
    // ============================================================

    public bool TieneImagen
    {
        get
        {
            return !string.IsNullOrWhiteSpace(
                image);
        }
    }

    // ============================================================
    // COPIA
    // ============================================================

    public PvZReanimFrame Copiar()
    {
        return new PvZReanimFrame
        {
            frameNumber =
                frameNumber,

            x =
                x,

            y =
                y,

            kx =
                kx,

            ky =
                ky,

            sx =
                sx,

            sy =
                sy,

            rotation =
                rotation,

            alpha =
                alpha,

            image =
                image,

            tieneTransformacion =
                tieneTransformacion
        };
    }

    // ============================================================
    // DEBUG
    // ============================================================

    public override string ToString()
    {
        return
            "Frame=" +
            frameNumber +

            " X=" +
            x +

            " Y=" +
            y +

            " KX=" +
            kx +

            " KY=" +
            ky +

            " SX=" +
            sx +

            " SY=" +
            sy +

            " R=" +
            rotation +

            " A=" +
            alpha +

            " IMAGE=" +
            image;
    }
}