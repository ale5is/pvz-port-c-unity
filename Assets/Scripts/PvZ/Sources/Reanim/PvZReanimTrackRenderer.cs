using UnityEngine;

/// <summary>
/// Renderiza una pieza individual del REANIM.
/// </summary>
public class PvZReanimTrackRenderer : MonoBehaviour
{
    private PvZReanimRenderer propietario;
    private PvZReanimTrack track;
    private SpriteRenderer spriteRenderer;

    private string ultimaImagen;
    private int indiceTrack;

    // ============================================================
    // INICIALIZAR
    // ============================================================

    public void Inicializar(
        PvZReanimRenderer propietario,
        PvZReanimTrack track,
        SpriteRenderer spriteRenderer,
        int indice)
    {
        this.propietario = propietario;
        this.track = track;
        this.spriteRenderer = spriteRenderer;
        this.indiceTrack = indice;

        if (this.spriteRenderer != null)
        {
            this.spriteRenderer.drawMode =
                SpriteDrawMode.Simple;

            this.spriteRenderer.enabled =
                false;

            this.spriteRenderer.sortingOrder =
                indice;
        }

        ultimaImagen = null;
    }

    // ============================================================
    // APLICAR FRAME
    // ============================================================

    public void AplicarFrame(
        int indice,
        float escala)
    {
        if (track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            Desactivar();
            return;
        }

        PvZReanimFrame frame =
            ObtenerFrame(indice);

        if (frame == null)
        {
            Desactivar();
            return;
        }

        // ========================================================
        // IMAGEN
        // ========================================================

        string nombreImagen =
            frame.image;

        if (!string.IsNullOrWhiteSpace(nombreImagen))
        {
            ultimaImagen =
                nombreImagen.Trim();
        }
        else
        {
            nombreImagen =
                ultimaImagen;
        }

        if (!string.IsNullOrWhiteSpace(nombreImagen))
        {
            Sprite sprite =
                propietario.ObtenerSprite(
                    nombreImagen);

            if (sprite != null)
            {
                spriteRenderer.sprite =
                    sprite;

                spriteRenderer.enabled =
                    true;
            }
            else
            {
                Desactivar();
            }
        }
        else
        {
            Desactivar();
        }

        // ========================================================
        // POSICIÓN
        // ========================================================

        float x =
            frame.x * escala;

        float y =
            -frame.y * escala;

        transform.localPosition =
            new Vector3(
                x,
                y,
                0f);

        // ========================================================
        // ESCALA
        // ========================================================

        float sx =
            frame.sx;

        float sy =
            frame.sy;

        if (Mathf.Approximately(sx, 0f))
        {
            sx = 1f;
        }

        if (Mathf.Approximately(sy, 0f))
        {
            sy = 1f;
        }

        transform.localScale =
            new Vector3(
                sx,
                sy,
                1f);

        // ========================================================
        // ORDEN
        // ========================================================

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder =
                indiceTrack;
        }
    }

    // ============================================================
    // OBTENER FRAME
    // ============================================================

    private PvZReanimFrame ObtenerFrame(
        int indice)
    {
        if (track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return null;
        }

        if (indice < 0)
        {
            indice = 0;
        }

        if (indice >= track.frames.Count)
        {
            indice =
                track.frames.Count - 1;
        }

        return track.frames[indice];
    }

    // ============================================================
    // DESACTIVAR
    // ============================================================

    private void Desactivar()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = null;
        spriteRenderer.enabled = false;
    }

    // ============================================================
    // RESET
    // ============================================================

    public void ReiniciarImagen()
    {
        ultimaImagen = null;
        Desactivar();
    }

    // ============================================================
    // PROPIEDADES
    // ============================================================

    public PvZReanimTrack Track
    {
        get
        {
            return track;
        }
    }

    public SpriteRenderer SpriteRenderer
    {
        get
        {
            return spriteRenderer;
        }
    }

    public int IndiceTrack
    {
        get
        {
            return indiceTrack;
        }
    }
}