using System;
using UnityEngine;

/// <summary>
/// Renderiza una pieza individual de una REANIM.
///
/// Utiliza directamente PvZReanimFrame generado por
/// PvZReanimParser. No utiliza reflexión.
/// </summary>
public class PvZReanimTrackRenderer : MonoBehaviour
{
    // ============================================================
    // DATOS
    // ============================================================

    private PvZReanimRenderer propietario;

    private PvZReanimTrack track;

    private SpriteRenderer spriteRenderer;

    private string ultimaImagen;

    private int indiceTrack;

    private int ultimoFrame = -1;

    private bool inicializado;

    // ============================================================
    // INICIALIZAR
    // ============================================================

    public void Inicializar(
        PvZReanimRenderer propietario,
        PvZReanimTrack track,
        SpriteRenderer spriteRenderer,
        int indiceTrack)
    {
        this.propietario = propietario;
        this.track = track;
        this.spriteRenderer = spriteRenderer;
        this.indiceTrack = indiceTrack;

        inicializado = true;

        if (this.spriteRenderer != null)
        {
            this.spriteRenderer.enabled = false;

            // Los tracks posteriores se dibujan encima.
            this.spriteRenderer.sortingOrder = indiceTrack;
        }
    }

    // ============================================================
    // APLICAR FRAME
    // ============================================================

    public void AplicarFrame(
        int indiceFrame,
        float escala)
    {
        if (!inicializado)
        {
            return;
        }

        if (track == null ||
            spriteRenderer == null)
        {
            return;
        }

        if (track.frames == null ||
            track.frames.Count == 0)
        {
            spriteRenderer.enabled = false;
            return;
        }

        // --------------------------------------------------------
        // CLAMP
        // --------------------------------------------------------

        indiceFrame = Mathf.Clamp(
            indiceFrame,
            0,
            track.frames.Count - 1);

        PvZReanimFrame frame =
            track.frames[indiceFrame];

        if (frame == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        ultimoFrame = indiceFrame;

        // ========================================================
        // IMAGEN
        // ========================================================

        string nombreImagen =
            frame.image;

        if (!string.IsNullOrWhiteSpace(nombreImagen))
        {
            nombreImagen =
                nombreImagen.Trim();

            if (!string.Equals(
                ultimaImagen,
                nombreImagen,
                StringComparison.OrdinalIgnoreCase))
            {
                Sprite sprite =
                    propietario.ObtenerSprite(
                        nombreImagen);

                spriteRenderer.sprite = sprite;

                ultimaImagen = nombreImagen;
            }
            else if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite =
                    propietario.ObtenerSprite(
                        nombreImagen);
            }
        }
        else
        {
            spriteRenderer.sprite = null;
            ultimaImagen = null;
        }

        // ========================================================
        // VISIBILIDAD
        // ========================================================

        spriteRenderer.enabled =
            spriteRenderer.sprite != null;

        // ========================================================
        // POSICIÓN
        // ========================================================

        float x =
            frame.x;

        float y =
            frame.y;

        transform.localPosition =
            new Vector3(
                x * escala,
                -y * escala,
                0f);

        // ========================================================
        // ESCALA
        // ========================================================

        float escalaX =
            frame.sx;

        float escalaY =
            frame.sy;

        // Algunos REANIM pueden dejar los valores en cero
        // cuando no existe una transformación explícita.
        if (escalaX == 0f)
        {
            escalaX = 1f;
        }

        if (escalaY == 0f)
        {
            escalaY = 1f;
        }

        transform.localScale =
            new Vector3(
                escalaX,
                escalaY,
                1f);

        // ========================================================
        // ROTACIÓN
        // ========================================================

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -frame.f);
    }

    // ============================================================
    // DEBUG
    // ============================================================

    private void Start()
    {
        if (!inicializado)
        {
            Debug.LogWarning(
                "[PvZ Reanim Track] " +
                "TrackRenderer no fue inicializado. " +
                "Track=" +
                indiceTrack);
        }
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        propietario = null;
        track = null;
        spriteRenderer = null;

        ultimaImagen = null;

        inicializado = false;

        ultimoFrame = -1;
    }
}