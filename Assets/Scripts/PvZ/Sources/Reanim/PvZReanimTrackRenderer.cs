using UnityEngine;

/// <summary>
/// Renderer de un track individual de REANIM.
///
/// Cada track representa una pieza del personaje:
/// cabeza, boca, hojas, tallo, etc.
///
/// Se encarga de:
/// - Obtener la imagen del frame.
/// - Cargar el Sprite desde el PAK.
/// - Aplicar posición.
/// - Aplicar escala.
/// - Mantener la imagen anterior cuando el REANIM no especifica <i>.
/// - Aplicar el orden de dibujo.
/// </summary>
public class PvZReanimTrackRenderer : MonoBehaviour
{
    // =============================================================
    // DATOS
    // =============================================================

    private PvZReanimRenderer propietario;

    private PvZReanimTrack track;

    private SpriteRenderer spriteRenderer;

    private PvZReanimFrame ultimoFrameConImagen;

    private int indiceTrack;

    // =============================================================
    // INICIALIZAR
    // =============================================================

    public void Inicializar(
        PvZReanimRenderer propietario,
        PvZReanimTrack track,
        SpriteRenderer spriteRenderer)
    {
        this.propietario =
            propietario;

        this.track =
            track;

        this.spriteRenderer =
            spriteRenderer;

        // ---------------------------------------------------------
        // Configuración SpriteRenderer
        // ---------------------------------------------------------

        this.spriteRenderer.drawMode =
            SpriteDrawMode.Simple;

        this.spriteRenderer.enabled =
            false;

        // ---------------------------------------------------------
        // Orden inicial
        // ---------------------------------------------------------

        this.spriteRenderer.sortingLayerID =
            0;

        this.spriteRenderer.sortingOrder =
            0;

        // ---------------------------------------------------------
        // Estado
        // ---------------------------------------------------------

        ultimoFrameConImagen =
            null;
    }

    // =============================================================
    // CONFIGURAR ORDEN
    // =============================================================

    public void ConfigurarOrden(
        int orden)
    {
        indiceTrack =
            orden;

        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder =
            orden;
    }

    // =============================================================
    // APLICAR FRAME
    // =============================================================

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

        // ---------------------------------------------------------
        // Obtener frame
        // ---------------------------------------------------------

        PvZReanimFrame frame =
            ObtenerFrame(indice);

        if (frame == null)
        {
            Desactivar();

            return;
        }

        // ---------------------------------------------------------
        // IMAGEN
        // ---------------------------------------------------------

        string nombreImagen =
            ObtenerNombreImagen(frame);

        if (!string.IsNullOrWhiteSpace(
            nombreImagen))
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
                spriteRenderer.sprite =
                    null;

                spriteRenderer.enabled =
                    false;
            }
        }
        else
        {
            spriteRenderer.sprite =
                null;

            spriteRenderer.enabled =
                false;
        }

        // ---------------------------------------------------------
        // POSICIÓN
        // ---------------------------------------------------------

        float x =
            frame.x *
            escala;

        // ---------------------------------------------------------
        // PvZ / REANIM
        //
        // Y positivo normalmente apunta hacia abajo.
        //
        // Unity:
        // Y positivo apunta hacia arriba.
        // ---------------------------------------------------------

        float y =
            -frame.y *
            escala;

        transform.localPosition =
            new Vector3(
                x,
                y,
                0f);

        // ---------------------------------------------------------
        // ESCALA
        // ---------------------------------------------------------

        float sx =
            frame.sx;

        float sy =
            frame.sy;

        // Algunos frames pueden utilizar 0
        // para indicar el valor por defecto.

        if (Mathf.Approximately(
            sx,
            0f))
        {
            sx = 1f;
        }

        if (Mathf.Approximately(
            sy,
            0f))
        {
            sy = 1f;
        }

        transform.localScale =
            new Vector3(
                sx,
                sy,
                1f);
    }

    // =============================================================
    // OBTENER IMAGEN
    // =============================================================

    private string ObtenerNombreImagen(
        PvZReanimFrame frame)
    {
        if (frame == null)
        {
            return null;
        }

        // ---------------------------------------------------------
        // Este frame tiene imagen explícita.
        // ---------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
            frame.image))
        {
            ultimoFrameConImagen =
                frame;

            return frame.image.Trim();
        }

        // ---------------------------------------------------------
        // El REANIM puede mantener la imagen anterior.
        // ---------------------------------------------------------

        if (ultimoFrameConImagen != null &&
            !string.IsNullOrWhiteSpace(
                ultimoFrameConImagen.image))
        {
            return
                ultimoFrameConImagen.image.Trim();
        }

        return null;
    }

    // =============================================================
    // OBTENER FRAME
    // =============================================================

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
            return null;
        }

        // ---------------------------------------------------------
        // El track puede tener menos frames.
        //
        // En ese caso mantenemos el último.
        // ---------------------------------------------------------

        if (indice >= track.frames.Count)
        {
            return
                track.frames[
                    track.frames.Count - 1];
        }

        return
            track.frames[indice];
    }

    // =============================================================
    // DESACTIVAR
    // =============================================================

    private void Desactivar()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite =
            null;

        spriteRenderer.enabled =
            false;
    }

    // =============================================================
    // PROPIEDADES
    // =============================================================

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

    // =============================================================
    // RESET
    // =============================================================

    public void ReiniciarImagen()
    {
        ultimoFrameConImagen =
            null;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite =
                null;

            spriteRenderer.enabled =
                false;
        }
    }
}