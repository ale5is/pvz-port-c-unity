using System;
using UnityEngine;

public class PvZReanimTrackRenderer : MonoBehaviour
{
    private PvZReanimRenderer propietario;

    private PvZReanimTrack track;

    private SpriteRenderer spriteRenderer;

    private string ultimaImagen;

    private int indiceTrack;

    private bool inicializado;

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

            this.spriteRenderer.sortingOrder =
                indiceTrack;
        }
    }

    public void AplicarFrame(
        int indiceFrame,
        float escala)
    {
        if (!inicializado ||
            track == null ||
            spriteRenderer == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return;
        }

        indiceFrame =
            Mathf.Clamp(
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

        // --------------------------------------------------------
        // SPRITE
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
            frame.image))
        {
            if (!string.Equals(
                ultimaImagen,
                frame.image,
                StringComparison.OrdinalIgnoreCase))
            {
                spriteRenderer.sprite =
                    propietario.ObtenerSprite(
                        frame.image);

                ultimaImagen =
                    frame.image;
            }
        }
        else
        {
            spriteRenderer.sprite = null;
            ultimaImagen = null;
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        spriteRenderer.enabled = true;

        // --------------------------------------------------------
        // TRANSFORMACIÓN
        // --------------------------------------------------------

        transform.localPosition =
            new Vector3(
                frame.x * escala,
                -frame.y * escala,
                0f);

        float sx =
            frame.sx;

        float sy =
            frame.sy;

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

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -frame.rotation);

        // --------------------------------------------------------
        // ALPHA
        // --------------------------------------------------------

        Color color =
            spriteRenderer.color;

        color.a =
            Mathf.Clamp01(
                frame.alpha);

        spriteRenderer.color =
            color;
    }

    public void AplicarTiempo(
        float tiempoFrames,
        float escala)
    {
        if (!inicializado ||
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return;
        }

        if (track.frames.Count == 1)
        {
            AplicarFrame(
                0,
                escala);

            return;
        }

        // --------------------------------------------------------
        // Encontrar frame anterior/siguiente.
        // --------------------------------------------------------

        PvZReanimFrame anterior =
            track.frames[0];

        PvZReanimFrame siguiente =
            track.frames[
                track.frames.Count - 1];

        for (
            int i = 0;
            i < track.frames.Count;
            i++)
        {
            PvZReanimFrame actual =
                track.frames[i];

            if (actual.frameNumber <=
                tiempoFrames)
            {
                anterior = actual;
            }

            if (actual.frameNumber >=
                tiempoFrames)
            {
                siguiente = actual;
                break;
            }
        }

        if (anterior == null ||
            siguiente == null)
        {
            return;
        }

        float rango =
            siguiente.frameNumber -
            anterior.frameNumber;

        float t;

        if (Mathf.Approximately(
            rango,
            0f))
        {
            t = 0f;
        }
        else
        {
            t =
                Mathf.InverseLerp(
                    anterior.frameNumber,
                    siguiente.frameNumber,
                    tiempoFrames);
        }

        // --------------------------------------------------------
        // Sprite:
        // usamos el sprite del frame anterior.
        // --------------------------------------------------------

        string imagen =
            !string.IsNullOrWhiteSpace(
                siguiente.image)
                ? siguiente.image
                : anterior.image;

        if (!string.Equals(
            ultimaImagen,
            imagen,
            StringComparison.OrdinalIgnoreCase))
        {
            spriteRenderer.sprite =
                propietario.ObtenerSprite(
                    imagen);

            ultimaImagen =
                imagen;
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        spriteRenderer.enabled = true;

        // --------------------------------------------------------
        // INTERPOLACIÓN
        // --------------------------------------------------------

        float x =
            Mathf.Lerp(
                anterior.x,
                siguiente.x,
                t);

        float y =
            Mathf.Lerp(
                anterior.y,
                siguiente.y,
                t);

        float sx =
            Mathf.Lerp(
                anterior.sx,
                siguiente.sx,
                t);

        float sy =
            Mathf.Lerp(
                anterior.sy,
                siguiente.sy,
                t);

        float rotacion =
            Mathf.LerpAngle(
                anterior.rotation,
                siguiente.rotation,
                t);

        float alpha =
            Mathf.Lerp(
                anterior.alpha,
                siguiente.alpha,
                t);

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

        transform.localPosition =
            new Vector3(
                x * escala,
                -y * escala,
                0f);

        transform.localScale =
            new Vector3(
                sx,
                sy,
                1f);

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -rotacion);

        Color color =
            spriteRenderer.color;

        color.a =
            Mathf.Clamp01(alpha);

        spriteRenderer.color =
            color;
    }

    private void OnDestroy()
    {
        propietario = null;
        track = null;
        spriteRenderer = null;
        ultimaImagen = null;
        inicializado = false;
    }
}