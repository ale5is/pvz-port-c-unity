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

    private Vector3[] verticesBase;

    private int indiceTrack;

    private bool inicializado;

    private string ultimaImagen;

    private int ultimoImageFrame =
        int.MinValue;

    private Texture2D ultimaTextura;

    // ============================================================
    // INICIALIZAR
    // ============================================================

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

    // ============================================================
    // MATERIAL
    // ============================================================

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

        Material resultado =
            new Material(shader);

        resultado.name =
            "REANIM_Track_Material_" +
            indiceTrack;

        resultado.renderQueue =
            3000;

        return resultado;
    }

    // ============================================================
    // APLICAR TIEMPO
    // ============================================================

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

        int cantidad =
            track.frames.Count;

        // ========================================================
        // Resodded utiliza:
        //
        // frame anterior
        // frame siguiente
        // fracción
        //
        // ========================================================

        float tiempo =
            Mathf.Clamp(
                tiempoFrames,
                0f,
                Mathf.Max(
                    0,
                    cantidad - 1));

        int indiceAntes =
            Mathf.FloorToInt(
                tiempo);

        int indiceDespues =
            Mathf.Min(
                indiceAntes + 1,
                cantidad - 1);

        float fraccion =
            tiempo -
            indiceAntes;

        PvZReanimFrame anterior =
            track.frames[
                indiceAntes];

        PvZReanimFrame siguiente =
            track.frames[
                indiceDespues];

        if (
            anterior == null ||
            siguiente == null)
        {
            Ocultar();

            return;
        }

        // ========================================================
        // IMAGEN
        //
        // Resodded utiliza la imagen del frame anterior.
        // ========================================================

        if (
            anterior.imageFrame < 0 ||
            string.IsNullOrWhiteSpace(
                anterior.image))
        {
            Ocultar();

            return;
        }

        string imagen =
            anterior.image;

        int imageFrame =
            anterior.imageFrame;

        if (!AplicarImagen(
            imagen,
            imageFrame))
        {
            Ocultar();

            return;
        }

        // ========================================================
        // TRANSFORMACIÓN
        // ========================================================

        AplicarTransformacion(
            anterior,
            siguiente,
            fraccion,
            escala);

        // ========================================================
        // ALPHA
        // ========================================================

        float alpha =
            Mathf.Lerp(
                anterior.alpha,
                siguiente.alpha,
                fraccion);

        AplicarAlpha(
            alpha);

        meshRenderer.enabled =
            true;
    }

    // ============================================================
    // IMAGEN
    // ============================================================

    private bool AplicarImagen(
        string imagen,
        int imageFrame)
    {
        if (
            propietario == null ||
            string.IsNullOrWhiteSpace(
                imagen))
        {
            return false;
        }

        // ========================================================
        // Si no cambió imagen/cel,
        // no reconstruimos.
        // ========================================================

        if (
            string.Equals(
                ultimaImagen,
                imagen,
                StringComparison.OrdinalIgnoreCase) &&
            ultimoImageFrame ==
            imageFrame)
        {
            return
                meshRenderer.sharedMaterial != null &&
                mesh.vertexCount == 4;
        }

        Texture2D textura;
        Rect rect;
        int width;
        int height;

        int maxFrame =
            propietario.ObtenerMaxImageFrame(
                imagen);

        bool atlas =
            propietario.Atlas.TryGet(
                imagen,
                out textura,
                out rect,
                out width,
                out height);

        if (!atlas)
        {
            if (!propietario.Atlas.TryGetIndividual(
                imagen,
                propietario.CargarTexturaParaTrack,
                out textura,
                out rect,
                out width,
                out height))
            {
                return false;
            }
        }

        if (textura == null)
        {
            return false;
        }

        // ========================================================
        // Determinar cantidad de celdas.
        //
        // Resodded solo mete al atlas imágenes 1x1.
        //
        // Para imágenes individuales con f > 0,
        // inferimos columnas.
        // ========================================================

        int columnas = 1;

        if (!atlas && maxFrame > 0)
        {
            columnas =
                maxFrame + 1;

            if (
                columnas <= 0 ||
                width % columnas != 0)
            {
                columnas = 1;
            }
        }

        int celWidth =
            width /
            columnas;

        int celHeight =
            height;

        int cel =
            Mathf.Max(
                0,
                imageFrame);

        if (cel >= columnas)
        {
            cel =
                columnas - 1;
        }

        Rect region;

        if (columnas == 1)
        {
            region =
                rect;
        }
        else
        {
            region =
                new Rect(
                    rect.x +
                    cel *
                    celWidth,

                    rect.y,

                    celWidth,
                    celHeight);
        }

        ultimaImagen =
            imagen;

        ultimoImageFrame =
            imageFrame;

        ultimaTextura =
            textura;

        if (material != null)
        {
            material.mainTexture =
                textura;
        }

        ConstruirMesh(
            region,
            textura.width,
            textura.height,
            celWidth,
            celHeight);

        return true;
    }

    // ============================================================
    // CREAR MESH
    // ============================================================

    private void ConstruirMesh(
        Rect region,
        int textureWidth,
        int textureHeight,
        int width,
        int height)
    {
        if (mesh == null)
        {
            return;
        }

        mesh.Clear();

        // Igual que un Sprite con pivot 0.5.
        float ancho =
            width /
            100f;

        float alto =
            height /
            100f;

        float pivotX =
            ancho *
            0.5f;

        float pivotY =
            alto *
            0.5f;

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

        float uMin =
            region.xMin /
            textureWidth;

        float uMax =
            region.xMax /
            textureWidth;

        float vMin =
            region.yMin /
            textureHeight;

        float vMax =
            region.yMax /
            textureHeight;

        Vector2[] uv =
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
            uv;

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

        mesh.RecalculateBounds();
    }

    // ============================================================
    // TRANSFORMACIÓN
    // ============================================================

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

        float kx =
            Mathf.Lerp(
                anterior.kx,
                siguiente.kx,
                t);

        float ky =
            Mathf.Lerp(
                anterior.ky,
                siguiente.ky,
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

        // ========================================================
        // MATRIZ DE RESODDED
        //
        // m00 = cos(-kx) * sx
        // m10 = -sin(-kx) * sx
        // m01 = sin(-ky) * sy
        // m11 = cos(-ky) * sy
        //
        // ========================================================

        float kxRad =
            -kx *
            Mathf.Deg2Rad;

        float kyRad =
            -ky *
            Mathf.Deg2Rad;

        float m00 =
            Mathf.Cos(kxRad) *
            sx;

        float m10 =
            -Mathf.Sin(kxRad) *
            sx;

        float m01 =
            Mathf.Sin(kyRad) *
            sy;

        float m11 =
            Mathf.Cos(kyRad) *
            sy;

        if (
            mesh != null &&
            verticesBase != null &&
            verticesBase.Length == 4)
        {
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

                vertices[i] =
                    new Vector3(
                        m00 * v.x +
                        m01 * v.y,

                        m10 * v.x +
                        m11 * v.y,

                        0f);
            }

            mesh.vertices =
                vertices;

            mesh.RecalculateBounds();
        }

        // PvZ Y positivo va hacia arriba en su sistema,
        // mientras que usamos el equivalente visual 2D.
        transform.localPosition =
            new Vector3(
                x * escala,
                -y * escala,
                0f);

        transform.localRotation =
            Quaternion.identity;

        transform.localScale =
            Vector3.one;
    }

    // ============================================================
    // ALPHA
    // ============================================================

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

    // ============================================================
    // OCULTAR
    // ============================================================

    private void Ocultar()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled =
                false;
        }
    }

    // ============================================================
    // COMPATIBILIDAD
    // ============================================================

    public void AplicarFrame(
        int indiceFrame,
        float escala)
    {
        AplicarTiempo(
            indiceFrame,
            escala);
    }

    // ============================================================
    // LIMPIAR
    // ============================================================

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
        verticesBase = null;
        ultimaImagen = null;
        ultimaTextura = null;
        ultimoImageFrame =
            int.MinValue;
        inicializado = false;
    }
}