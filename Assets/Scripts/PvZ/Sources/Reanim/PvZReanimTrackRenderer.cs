using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PvZReanimTrackRenderer : MonoBehaviour
{
    private PvZReanimRenderer propietario;

    private PvZReanimTrack track;

    private MeshFilter meshFilter;

    private MeshRenderer meshRenderer;

    private Mesh mesh;

    private Material material;

    private Texture2D ultimaTextura;

    private string ultimaImagen;

    private Vector3[] verticesBase;

    private int indiceTrack;

    private bool inicializado;

    public void Inicializar(
        PvZReanimRenderer propietario,
        PvZReanimTrack track,
        SpriteRenderer spriteRenderer,
        int indiceTrack)
    {
        this.propietario =
            propietario;

        this.track =
            track;

        this.indiceTrack =
            indiceTrack;

        meshFilter =
            GetComponent<MeshFilter>();

        meshRenderer =
            GetComponent<MeshRenderer>();

        mesh =
            new Mesh();

        mesh.name =
            "REANIM_Track_" +
            indiceTrack;

        mesh.MarkDynamic();

        meshFilter.sharedMesh =
            mesh;

        material =
            CrearMaterial();

        if (material != null)
        {
            meshRenderer.sharedMaterial =
                material;
        }

        meshRenderer.sortingOrder =
            indiceTrack;

        inicializado =
            true;

        Ocultar();
    }

    private Material CrearMaterial()
    {
        Shader shader =
            Shader.Find(
                "Sprites/Default");

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Transparent");
        }

        if (shader == null)
        {
            Debug.LogError(
                "[PvZ Reanim] " +
                "No se encontró shader.");

            return null;
        }

        Material nuevo =
            new Material(shader);

        nuevo.name =
            "REANIM_Track_Material_" +
            indiceTrack;

        nuevo.renderQueue =
            3000;

        return nuevo;
    }

    public void AplicarTiempo(
        float tiempoFrames,
        float escala)
    {
        if (
            !inicializado ||
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return;
        }

        PvZReanimFrame anterior = null;

        PvZReanimFrame siguiente = null;

        for (
            int i = 0;
            i < track.frames.Count;
            i++)
        {
            PvZReanimFrame actual =
                track.frames[i];

            if (actual == null)
            {
                continue;
            }

            if (
                actual.frameNumber <=
                tiempoFrames)
            {
                anterior =
                    actual;
            }

            if (
                actual.frameNumber >=
                tiempoFrames)
            {
                siguiente =
                    actual;

                break;
            }
        }

        if (anterior == null)
        {
            anterior =
                ObtenerPrimerFrame();

            siguiente =
                anterior;
        }

        if (siguiente == null)
        {
            siguiente =
                ObtenerUltimoFrame();

            anterior =
                siguiente;
        }

        if (
            anterior == null &&
            siguiente == null)
        {
            Ocultar();

            return;
        }

        if (anterior == null)
        {
            anterior =
                siguiente;
        }

        if (siguiente == null)
        {
            siguiente =
                anterior;
        }

        float rango =
            siguiente.frameNumber -
            anterior.frameNumber;

        float t;

        if (
            Mathf.Approximately(
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

        t =
            Mathf.Clamp01(t);

        string imagen =
            ObtenerImagenActual(
                anterior,
                siguiente,
                t);

        if (string.IsNullOrWhiteSpace(imagen))
        {
            Ocultar();

            return;
        }

        Sprite sprite =
            propietario.ObtenerSprite(
                imagen);

        if (sprite == null)
        {
            Ocultar();

            return;
        }

        AplicarSprite(
            sprite,
            imagen);

        AplicarTransformacion(
            anterior,
            siguiente,
            t,
            escala);

        float alpha =
            Mathf.Lerp(
                anterior.alpha,
                siguiente.alpha,
                t);

        AplicarAlpha(
            alpha);

        meshRenderer.enabled =
            true;
    }

    private void AplicarSprite(
        Sprite sprite,
        string imagen)
    {
        if (
            mesh == null ||
            sprite == null)
        {
            return;
        }

        Texture2D textura =
            sprite.texture;

        if (textura == null)
        {
            Ocultar();

            return;
        }

        if (
            ultimaTextura != textura ||
            !string.Equals(
                ultimaImagen,
                imagen,
                StringComparison.OrdinalIgnoreCase))
        {
            ultimaTextura =
                textura;

            ultimaImagen =
                imagen;

            if (material != null)
            {
                material.mainTexture =
                    textura;
            }

            ConstruirMesh(
                sprite);
        }
    }

    private void ConstruirMesh(
        Sprite sprite)
    {
        if (
            mesh == null ||
            sprite == null)
        {
            return;
        }

        mesh.Clear();

        Rect rect =
            sprite.rect;

        Vector2 pivot =
            sprite.pivot;

        float pixelsPerUnit =
            sprite.pixelsPerUnit;

        if (
            Mathf.Approximately(
                pixelsPerUnit,
                0f))
        {
            pixelsPerUnit =
                100f;
        }

        float ancho =
            rect.width /
            pixelsPerUnit;

        float alto =
            rect.height /
            pixelsPerUnit;

        float pivotX =
            pivot.x /
            pixelsPerUnit;

        float pivotY =
            pivot.y /
            pixelsPerUnit;

        Vector3[] vertices =
        {
            new Vector3(
                -pivotX,
                -pivotY,
                0f),

            new Vector3(
                ancho - pivotX,
                -pivotY,
                0f),

            new Vector3(
                ancho - pivotX,
                alto - pivotY,
                0f),

            new Vector3(
                -pivotX,
                alto - pivotY,
                0f)
        };

        verticesBase =
            (Vector3[])vertices.Clone();

        mesh.vertices =
            vertices;

        Rect uvRect =
            sprite.textureRect;

        float textureWidth =
            sprite.texture.width;

        float textureHeight =
            sprite.texture.height;

        float uMin =
            uvRect.xMin /
            textureWidth;

        float uMax =
            uvRect.xMax /
            textureWidth;

        float vMin =
            uvRect.yMin /
            textureHeight;

        float vMax =
            uvRect.yMax /
            textureHeight;

        Vector2[] uvs =
        {
            new Vector2(
                uMin,
                vMin),

            new Vector2(
                uMax,
                vMin),

            new Vector2(
                uMax,
                vMax),

            new Vector2(
                uMin,
                vMax)
        };

        mesh.uv =
            uvs;

        mesh.triangles =
            new int[]
            {
                0, 2, 1,
                0, 3, 2
            };

        mesh.colors =
            new Color[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };

        mesh.RecalculateNormals();

        mesh.RecalculateBounds();
    }

    private void AplicarTransformacion(
        PvZReanimFrame anterior,
        PvZReanimFrame siguiente,
        float t,
        float escala)
    {
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

        transform.localPosition =
            new Vector3(
                x * escala,
                -y * escala,
                0f);

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

        if (
            Mathf.Approximately(
                sx,
                0f))
        {
            sx = 1f;
        }

        if (
            Mathf.Approximately(
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

        float kx =
            Mathf.LerpAngle(
                anterior.kx,
                siguiente.kx,
                t);

        float ky =
            Mathf.LerpAngle(
                anterior.ky,
                siguiente.ky,
                t);

        float skew =
            ky - kx;

        float rotacion =
            kx;

        if (
            Mathf.Approximately(
                anterior.kx,
                0f) &&
            Mathf.Approximately(
                siguiente.kx,
                0f) &&
            (
                !Mathf.Approximately(
                    anterior.rotation,
                    0f) ||
                !Mathf.Approximately(
                    siguiente.rotation,
                    0f)
            ))
        {
            rotacion =
                Mathf.LerpAngle(
                    anterior.rotation,
                    siguiente.rotation,
                    t);
        }

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -rotacion);

        AplicarSkew(
            skew);
    }

    private void AplicarSkew(
        float grados)
    {
        if (
            mesh == null ||
            verticesBase == null ||
            verticesBase.Length != 4)
        {
            return;
        }

        float radianes =
            grados *
            Mathf.Deg2Rad;

        float shear =
            Mathf.Tan(
                radianes);

        Vector3[] vertices =
            new Vector3[
                verticesBase.Length];

        for (
            int i = 0;
            i < verticesBase.Length;
            i++)
        {
            Vector3 v =
                verticesBase[i];

            v.x =
                verticesBase[i].x +
                verticesBase[i].y *
                shear;

            vertices[i] =
                v;
        }

        mesh.vertices =
            vertices;

        mesh.RecalculateBounds();
    }

    private void AplicarAlpha(
        float alpha)
    {
        if (material == null)
        {
            return;
        }

        alpha =
            Mathf.Clamp01(
                alpha);

        if (
            material.HasProperty(
                "_Color"))
        {
            Color color =
                Color.white;

            color.a =
                alpha;

            material.color =
                color;
        }
    }

    private string ObtenerImagenActual(
        PvZReanimFrame anterior,
        PvZReanimFrame siguiente,
        float t)
    {
        if (
            anterior == null &&
            siguiente == null)
        {
            return null;
        }

        if (anterior == null)
        {
            return siguiente.image;
        }

        if (siguiente == null)
        {
            return anterior.image;
        }

        if (
            anterior.frameNumber ==
            siguiente.frameNumber)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    siguiente.image))
            {
                return siguiente.image;
            }

            return anterior.image;
        }

        if (t < 1f)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    anterior.image))
            {
                return anterior.image;
            }

            return siguiente.image;
        }

        if (
            !string.IsNullOrWhiteSpace(
                siguiente.image))
        {
            return siguiente.image;
        }

        return anterior.image;
    }

    private PvZReanimFrame ObtenerPrimerFrame()
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return null;
        }

        PvZReanimFrame resultado = null;

        foreach (
            PvZReanimFrame frame
            in track.frames)
        {
            if (frame == null)
            {
                continue;
            }

            if (
                resultado == null ||
                frame.frameNumber <
                resultado.frameNumber)
            {
                resultado =
                    frame;
            }
        }

        return resultado;
    }

    private PvZReanimFrame ObtenerUltimoFrame()
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return null;
        }

        PvZReanimFrame resultado = null;

        foreach (
            PvZReanimFrame frame
            in track.frames)
        {
            if (frame == null)
            {
                continue;
            }

            if (
                resultado == null ||
                frame.frameNumber >
                resultado.frameNumber)
            {
                resultado =
                    frame;
            }
        }

        return resultado;
    }

    private void Ocultar()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled =
                false;
        }
    }

    public void AplicarFrame(
        int indiceFrame,
        float escala)
    {
        AplicarTiempo(
            indiceFrame,
            escala);
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }

        if (material != null)
        {
            Destroy(material);
        }

        propietario = null;
        track = null;
        meshFilter = null;
        meshRenderer = null;
        mesh = null;
        material = null;
        ultimaTextura = null;
        ultimaImagen = null;
        verticesBase = null;
        inicializado = false;
    }
}