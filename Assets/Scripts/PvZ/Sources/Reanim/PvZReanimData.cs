using System;
using System.Collections.Generic;

/// <summary>
/// Datos completos de una animación REANIM de Plants vs. Zombies.
/// </summary>
[Serializable]
public class PvZReanimData
{
    /// <summary>
    /// FPS originales de la animación.
    /// </summary>
    public float fps = 12f;

    /// <summary>
    /// Tracks que forman la animación.
    /// </summary>
    public List<PvZReanimTrack> tracks = new List<PvZReanimTrack>();
}

/// <summary>
/// Un track de REANIM.
/// Cada track representa una parte del personaje.
/// Por ejemplo: backleaf, head, mouth, stalk, etc.
/// </summary>
[Serializable]
public class PvZReanimTrack
{
    /// <summary>
    /// Nombre del track.
    /// </summary>
    public string name;

    /// <summary>
    /// Frames del track.
    /// </summary>
    public List<PvZReanimFrame> frames =
        new List<PvZReanimFrame>();
}

/// <summary>
/// Un frame individual de un track REANIM.
/// </summary>
[Serializable]
public class PvZReanimFrame
{
    /// <summary>
    /// Posición X.
    /// </summary>
    public float x;

    /// <summary>
    /// Posición Y.
    /// </summary>
    public float y;

    /// <summary>
    /// Escala X.
    /// </summary>
    public float sx = 1f;

    /// <summary>
    /// Escala Y.
    /// </summary>
    public float sy = 1f;

    /// <summary>
    /// Índice/frame de imagen.
    /// En los REANIM aparece como <f>.
    /// </summary>
    public int f = -1;

    /// <summary>
    /// Nombre de la imagen.
    /// En los REANIM aparece como <i>.
    /// </summary>
    public string image;

    /// <summary>
    /// Indica si este frame tiene información
    /// explícita de transformación.
    /// </summary>
    public bool tieneTransformacion;

    /// <summary>
    /// Indica si este frame contiene una imagen.
    /// </summary>
    public bool TieneImagen =>
        !string.IsNullOrEmpty(image);

    public override string ToString()
    {
        return
            $"X={x} " +
            $"Y={y} " +
            $"SX={sx} " +
            $"SY={sy} " +
            $"F={f} " +
            $"IMAGE={image}";
    }
}