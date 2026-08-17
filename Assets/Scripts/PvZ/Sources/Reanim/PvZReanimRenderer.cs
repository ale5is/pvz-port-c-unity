using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renderer principal de una animación REANIM de Plants vs. Zombies.
///
/// Se encarga de:
/// - Cargar el REANIM desde el PAK.
/// - Crear los tracks.
/// - Obtener las imágenes desde el PAK.
/// - Controlar la reproducción.
/// - Enviar cada frame a PvZReanimTrackRenderer.
/// </summary>
public class PvZReanimRenderer : MonoBehaviour
{
    // ============================================================
    // CONFIGURACIÓN
    // ============================================================

    [Header("REANIM")]
    [SerializeField]
    private string rutaReanim =
        "REANIM/PEASHOOTER.REANIM";

    [Header("Escala")]
    [SerializeField]
    private float escala =
        0.01f;

    [Header("Velocidad")]
    [SerializeField]
    private float multiplicadorVelocidad =
        1f;

    [Header("Animación")]
    [SerializeField]
    private bool reproducirAutomaticamente =
        true;

    [Header("Debug")]
    [SerializeField]
    private bool mostrarDebug =
        true;

    // ============================================================
    // DATOS
    // ============================================================

    private PvZReanimData reanim;

    private UnityEngine.GameObject raiz;

    private readonly Dictionary<string, Sprite> sprites =
        new Dictionary<string, Sprite>(
            StringComparer.OrdinalIgnoreCase);

    private readonly List<PvZReanimTrackRenderer> renderTracks =
        new List<PvZReanimTrackRenderer>();

    private float tiempo;

    private int frameActual;

    private int cantidadFrames;

    private bool listo;

    // ============================================================
    // START
    // ============================================================

    private IEnumerator Start()
    {
        while (
            PvZResourceManager.Instancia == null ||
            !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] ResourceManager listo.");
        }

        if (!CargarReanim())
        {
            yield break;
        }

        CrearRenderer();

        listo = true;

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] Renderer listo: " +
                rutaReanim);
        }
    }

    // ============================================================
    // CARGAR REANIM
    // ============================================================

    private bool CargarReanim()
    {
        if (string.IsNullOrWhiteSpace(rutaReanim))
        {
            Debug.LogError(
                "[PvZ Reanim] Ruta vacía.");

            return false;
        }

        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ Reanim] ResourceManager no existe.");

            return false;
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                rutaReanim);

        if (datos == null ||
            datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ Reanim] No se encontró: " +
                rutaReanim);

            return false;
        }

        try
        {
            reanim =
                PvZReanimParser.Parse(
                    datos);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ Reanim] Error parser:\n" +
                e);

            return false;
        }

        if (reanim == null)
        {
            Debug.LogError(
                "[PvZ Reanim] Parser devolvió null.");

            return false;
        }

        cantidadFrames =
            ObtenerCantidadFrames();

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] " +
                "FPS=" +
                reanim.fps +
                " Tracks=" +
                reanim.tracks.Count +
                " Frames=" +
                cantidadFrames);
        }

        return true;
    }

    // ============================================================
    // FRAMES
    // ============================================================

    private int ObtenerCantidadFrames()
    {
        int max =
            0;

        if (reanim == null ||
            reanim.tracks == null)
        {
            return 0;
        }

        foreach (
            PvZReanimTrack track
            in reanim.tracks)
        {
            if (track == null ||
                track.frames == null)
            {
                continue;
            }

            if (track.frames.Count > max)
            {
                max =
                    track.frames.Count;
            }
        }

        return max;
    }

    // ============================================================
    // CREAR RENDERER
    // ============================================================

    private void CrearRenderer()
    {
        if (raiz != null)
        {
            Destroy(raiz);

            raiz =
                null;
        }

        renderTracks.Clear();

        raiz =
            new UnityEngine.GameObject(
                "REANIM_" +
                ObtenerNombreReanim());

        raiz.transform.SetParent(
            transform,
            false);

        raiz.transform.localPosition =
            Vector3.zero;

        raiz.transform.localRotation =
            Quaternion.identity;

        raiz.transform.localScale =
            Vector3.one;

        if (reanim == null ||
            reanim.tracks == null)
        {
            Debug.LogError(
                "[PvZ Reanim] No existen tracks.");

            return;
        }

        for (
            int i = 0;
            i < reanim.tracks.Count;
            i++)
        {
            PvZReanimTrack track =
                reanim.tracks[i];

            if (track == null)
            {
                continue;
            }

            CrearTrack(
                track,
                i);
        }

        AplicarFrame(0);
    }

    // ============================================================
    // CREAR TRACK
    // ============================================================

    private void CrearTrack(
        PvZReanimTrack track,
        int indice)
    {
        string nombre =
            string.IsNullOrWhiteSpace(track.name)
                ? "Track_" + indice
                : track.name;

        UnityEngine.GameObject objeto =
            new UnityEngine.GameObject(
                nombre);

        objeto.transform.SetParent(
            raiz.transform,
            false);

        objeto.transform.localPosition =
            Vector3.zero;

        objeto.transform.localRotation =
            Quaternion.identity;

        objeto.transform.localScale =
            Vector3.one;

        SpriteRenderer spriteRenderer =
            objeto.AddComponent<SpriteRenderer>();

        // Los tracks posteriores tienen prioridad visual.
        spriteRenderer.sortingOrder =
            indice;

        PvZReanimTrackRenderer rendererTrack =
            objeto.AddComponent<
                PvZReanimTrackRenderer>();

        rendererTrack.Inicializar(
            this,
            track,
            spriteRenderer,
            indice);

        renderTracks.Add(
            rendererTrack);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] Track " +
                indice +
                ": " +
                nombre +
                " (" +
                (
                    track.frames != null
                        ? track.frames.Count
                        : 0
                ) +
                " frames)");
        }
    }

    // ============================================================
    // APLICAR FRAME
    // ============================================================

    private void AplicarFrame(
        int indiceFrame)
    {
        if (cantidadFrames <= 0)
        {
            return;
        }

        indiceFrame =
            Mathf.Clamp(
                indiceFrame,
                0,
                cantidadFrames - 1);

        frameActual =
            indiceFrame;

        foreach (
            PvZReanimTrackRenderer track
            in renderTracks)
        {
            if (track == null)
            {
                continue;
            }

            track.AplicarFrame(
                indiceFrame,
                escala);
        }
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (!listo ||
            !reproducirAutomaticamente ||
            reanim == null ||
            cantidadFrames <= 0)
        {
            return;
        }

        float fps =
            reanim.fps > 0f
                ? reanim.fps
                : 12f;

        float velocidad =
            fps *
            multiplicadorVelocidad;

        if (velocidad <= 0f)
        {
            return;
        }

        tiempo +=
            Time.deltaTime *
            velocidad;

        while (tiempo >= 1f)
        {
            tiempo -= 1f;

            frameActual++;

            if (frameActual >= cantidadFrames)
            {
                frameActual = 0;
            }

            AplicarFrame(
                frameActual);
        }
    }

    // ============================================================
    // OBTENER SPRITE
    // ============================================================

    public Sprite ObtenerSprite(
        string nombreImagen)
    {
        if (string.IsNullOrWhiteSpace(nombreImagen))
        {
            return null;
        }

        nombreImagen =
            nombreImagen.Trim();

        Sprite sprite;

        if (sprites.TryGetValue(
            nombreImagen,
            out sprite))
        {
            return sprite;
        }

        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ Reanim] " +
                "ResourceManager no disponible.");

            return null;
        }

        string ruta =
            ConvertirImagenARuta(
                nombreImagen);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] Imagen: " +
                nombreImagen +
                " -> " +
                ruta);
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                ruta);

        if (datos == null ||
            datos.Length == 0)
        {
            Debug.LogWarning(
                "[PvZ Reanim] " +
                "Imagen no encontrada: " +
                ruta);

            return null;
        }

        Texture2D textura =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);

        textura.name =
            nombreImagen;

        bool cargada =
            textura.LoadImage(
                datos,
                false);

        if (!cargada)
        {
            Debug.LogError(
                "[PvZ Reanim] " +
                "No se pudo cargar: " +
                nombreImagen);

            Destroy(textura);

            return null;
        }

        // PvZ utiliza pixel art.
        textura.filterMode =
            FilterMode.Point;

        textura.wrapMode =
            TextureWrapMode.Clamp;

        // --------------------------------------------------------
        // SPRITE
        // --------------------------------------------------------
        //
        // Mantenemos el pivot central por ahora.
        // No lo cambiamos arbitrariamente hasta comprobar
        // los offsets reales del REANIM.
        // --------------------------------------------------------

        sprite =
            Sprite.Create(
                textura,
                new Rect(
                    0f,
                    0f,
                    textura.width,
                    textura.height),
                new Vector2(
                    0.5f,
                    0.5f),
                100f);

        sprite.name =
            nombreImagen;

        sprites[
            nombreImagen] =
            sprite;

        return sprite;
    }

    // ============================================================
    // CONVERTIR IMAGEN -> RUTA PAK
    // ============================================================

    private string ConvertirImagenARuta(
        string nombre)
    {
        string limpio =
            nombre.Trim();

        const string prefijo =
            "IMAGE_REANIM_";

        if (limpio.StartsWith(
            prefijo,
            StringComparison.OrdinalIgnoreCase))
        {
            limpio =
                limpio.Substring(
                    prefijo.Length);
        }

        limpio =
            limpio.Replace(
                "\\",
                "/");

        if (limpio.StartsWith(
            "REANIM/",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!limpio.EndsWith(
                ".PNG",
                StringComparison.OrdinalIgnoreCase))
            {
                limpio +=
                    ".PNG";
            }

            return limpio;
        }

        if (limpio.EndsWith(
            ".PNG",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                "REANIM/" +
                limpio;
        }

        return
            "REANIM/" +
            limpio +
            ".PNG";
    }

    // ============================================================
    // NOMBRE REANIM
    // ============================================================

    private string ObtenerNombreReanim()
    {
        string nombre =
            rutaReanim;

        int slash =
            nombre.LastIndexOf('/');

        if (slash >= 0)
        {
            nombre =
                nombre.Substring(
                    slash + 1);
        }

        int punto =
            nombre.LastIndexOf('.');

        if (punto >= 0)
        {
            nombre =
                nombre.Substring(
                    0,
                    punto);
        }

        return nombre;
    }

    // ============================================================
    // CONTROL
    // ============================================================

    public void Reproducir()
    {
        reproducirAutomaticamente =
            true;
    }

    public void Pausar()
    {
        reproducirAutomaticamente =
            false;
    }

    public void Reiniciar()
    {
        tiempo = 0f;

        AplicarFrame(0);
    }

    public void IrAFrame(
        int frame)
    {
        tiempo = 0f;

        AplicarFrame(
            frame);
    }

    // ============================================================
    // PROPIEDADES
    // ============================================================

    public int FrameActual
    {
        get
        {
            return frameActual;
        }
    }

    public int CantidadFrames
    {
        get
        {
            return cantidadFrames;
        }
    }

    public float FPS
    {
        get
        {
            return reanim != null
                ? reanim.fps
                : 12f;
        }
    }

    public PvZReanimData DatosReanim
    {
        get
        {
            return reanim;
        }
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        foreach (
            KeyValuePair<string, Sprite> entrada
            in sprites)
        {
            Sprite sprite =
                entrada.Value;

            if (sprite == null)
            {
                continue;
            }

            Texture2D textura =
                sprite.texture;

            Destroy(sprite);

            if (textura != null)
            {
                Destroy(textura);
            }
        }

        sprites.Clear();

        renderTracks.Clear();

        if (raiz != null)
        {
            Destroy(raiz);

            raiz =
                null;
        }

        reanim =
            null;

        listo =
            false;
    }
}