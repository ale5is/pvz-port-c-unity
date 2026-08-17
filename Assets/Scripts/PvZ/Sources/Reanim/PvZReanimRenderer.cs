using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renderer genérico para animaciones REANIM de Plants vs. Zombies.
///
/// Lee un PvZReanimData y crea un GameObject por cada track.
/// Cada track utiliza un SpriteRenderer.
///
/// Los REANIM y las imágenes se leen directamente
/// desde el PAK mediante PvZResourceManager.
/// </summary>
public class PvZReanimRenderer : MonoBehaviour
{
    // =============================================================
    // CONFIGURACIÓN
    // =============================================================

    [Header("REANIM")]
    [SerializeField]
    private string rutaReanim =
        "REANIM/PEASHOOTER.REANIM";

    [Header("Escala")]
    [SerializeField]
    private float escala = 0.01f;

    [Header("Velocidad")]
    [SerializeField]
    private float multiplicadorVelocidad = 1f;

    [Header("Animación")]
    [SerializeField]
    private bool reproducirAutomaticamente = true;

    [Header("Debug")]
    [SerializeField]
    private bool mostrarDebug = true;


    // =============================================================
    // DATOS
    // =============================================================

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


    // =============================================================
    // START
    // =============================================================

    private IEnumerator Start()
    {
        // ---------------------------------------------------------
        // Esperar a que el ResourceManager esté listo
        // ---------------------------------------------------------

        while (
            PvZResourceManager.Instancia == null ||
            !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] ResourceManager listo.");
        }

        // ---------------------------------------------------------
        // Cargar REANIM
        // ---------------------------------------------------------

        if (!CargarReanim())
        {
            yield break;
        }

        // ---------------------------------------------------------
        // Crear estructura visual
        // ---------------------------------------------------------

        CrearRenderer();

        listo = true;

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] " +
                "Renderer creado correctamente.");
        }
    }


    // =============================================================
    // CARGAR REANIM
    // =============================================================

    private bool CargarReanim()
    {
        if (string.IsNullOrWhiteSpace(rutaReanim))
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "La ruta REANIM está vacía.");

            return false;
        }

        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "PvZResourceManager no existe.");

            return false;
        }

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] " +
                "Cargando REANIM: " +
                rutaReanim);
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                rutaReanim);

        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "No se pudo leer: " +
                rutaReanim);

            return false;
        }

        // ---------------------------------------------------------
        // Parsear REANIM
        // ---------------------------------------------------------

        try
        {
            reanim =
                PvZReanimParser.Parse(datos);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "Error parseando REANIM:\n" +
                e);

            return false;
        }

        if (reanim == null)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "El parser devolvió null.");

            return false;
        }

        // ---------------------------------------------------------
        // Cantidad de frames
        // ---------------------------------------------------------

        cantidadFrames =
            ObtenerCantidadFrames();

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] " +
                "REANIM cargado. " +
                "FPS=" +
                reanim.fps +
                " Tracks=" +
                reanim.tracks.Count +
                " Frames=" +
                cantidadFrames);
        }

        return true;
    }


    // =============================================================
    // OBTENER CANTIDAD DE FRAMES
    // =============================================================

    private int ObtenerCantidadFrames()
    {
        int maximo = 0;

        if (reanim == null ||
            reanim.tracks == null)
        {
            return 0;
        }

        foreach (PvZReanimTrack track in reanim.tracks)
        {
            if (track == null ||
                track.frames == null)
            {
                continue;
            }

            if (track.frames.Count > maximo)
            {
                maximo =
                    track.frames.Count;
            }
        }

        return maximo;
    }


    // =============================================================
    // CREAR RENDERER
    // =============================================================

    private void CrearRenderer()
    {
        // ---------------------------------------------------------
        // Eliminar renderer anterior
        // ---------------------------------------------------------

        if (raiz != null)
        {
            Destroy(raiz);
            raiz = null;
        }

        renderTracks.Clear();

        // ---------------------------------------------------------
        // Crear raíz
        // ---------------------------------------------------------

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

        // ---------------------------------------------------------
        // Comprobar tracks
        // ---------------------------------------------------------

        if (reanim == null ||
            reanim.tracks == null)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "No existen tracks.");

            return;
        }

        // ---------------------------------------------------------
        // Crear cada track
        // ---------------------------------------------------------

        foreach (PvZReanimTrack track in reanim.tracks)
        {
            if (track == null)
            {
                continue;
            }

            CrearTrack(track);
        }

        // ---------------------------------------------------------
        // Aplicar primer frame
        // ---------------------------------------------------------

        AplicarFrame(0);
    }


    // =============================================================
    // CREAR TRACK
    // =============================================================

    private void CrearTrack(
        PvZReanimTrack track)
    {
        string nombre =
            string.IsNullOrWhiteSpace(track.name)
                ? "Track"
                : track.name;

        // IMPORTANTE:
        // UnityEngine.GameObject explícitamente.
        //
        // No usamos Unity.VisualScripting.GameObject.
        // ---------------------------------------------------------

        UnityEngine.GameObject objeto =
            new UnityEngine.GameObject(
                "Track_" +
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

        // ---------------------------------------------------------
        // SpriteRenderer de Unity
        // ---------------------------------------------------------

        SpriteRenderer spriteRenderer =
            objeto.AddComponent<SpriteRenderer>();

        // ---------------------------------------------------------
        // Renderer del track
        // ---------------------------------------------------------

        PvZReanimTrackRenderer rendererTrack =
            objeto.AddComponent<PvZReanimTrackRenderer>();

        rendererTrack.Inicializar(
            this,
            track,
            spriteRenderer);

        renderTracks.Add(
            rendererTrack);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] " +
                "Track creado: " +
                nombre +
                " Frames=" +
                (
                    track.frames != null
                        ? track.frames.Count
                        : 0
                ));
        }
    }


    // =============================================================
    // APLICAR FRAME
    // =============================================================

    private void AplicarFrame(
        int indiceFrame)
    {
        if (reanim == null)
        {
            return;
        }

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
            PvZReanimTrackRenderer trackRenderer
            in renderTracks)
        {
            if (trackRenderer == null)
            {
                continue;
            }

            trackRenderer.AplicarFrame(
                indiceFrame,
                escala);
        }
    }


    // =============================================================
    // UPDATE
    // =============================================================

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


    // =============================================================
    // OBTENER SPRITE
    // =============================================================

    public Sprite ObtenerSprite(
        string nombreImagen)
    {
        if (string.IsNullOrWhiteSpace(nombreImagen))
        {
            return null;
        }

        nombreImagen =
            nombreImagen.Trim();

        // ---------------------------------------------------------
        // CACHE
        // ---------------------------------------------------------

        Sprite sprite;

        if (sprites.TryGetValue(
            nombreImagen,
            out sprite))
        {
            return sprite;
        }

        // ---------------------------------------------------------
        // ResourceManager
        // ---------------------------------------------------------

        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ ReanimRenderer] " +
                "PvZResourceManager no está disponible.");

            return null;
        }

        // ---------------------------------------------------------
        // Convertir nombre REANIM -> ruta PAK
        // ---------------------------------------------------------

        string ruta =
            ConvertirImagenARuta(
                nombreImagen);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ ReanimRenderer] " +
                "Imagen: " +
                nombreImagen +
                " -> " +
                ruta);
        }

        // ---------------------------------------------------------
        // Leer imagen desde PAK
        // ---------------------------------------------------------

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                ruta);

        if (datos == null ||
            datos.Length == 0)
        {
            Debug.LogWarning(
                "[PvZ ReanimRenderer] " +
                "No se encontró imagen: " +
                nombreImagen +
                " -> " +
                ruta);

            return null;
        }

        // ---------------------------------------------------------
        // Crear textura
        // ---------------------------------------------------------

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
                "[PvZ ReanimRenderer] " +
                "No se pudo convertir PNG: " +
                nombreImagen);

            Destroy(textura);

            return null;
        }

        // ---------------------------------------------------------
        // Configuración de textura
        // ---------------------------------------------------------

        textura.filterMode =
            FilterMode.Point;

        textura.wrapMode =
            TextureWrapMode.Clamp;

        // ---------------------------------------------------------
        // Crear Sprite
        // ---------------------------------------------------------

        sprite =
            Sprite.Create(
                textura,
                new Rect(
                    0,
                    0,
                    textura.width,
                    textura.height),
                new Vector2(
                    0.5f,
                    0.5f),
                100f);

        sprite.name =
            nombreImagen;

        // ---------------------------------------------------------
        // Guardar en cache
        // ---------------------------------------------------------

        sprites[
            nombreImagen] =
            sprite;

        return sprite;
    }


    // =============================================================
    // CONVERTIR NOMBRE REANIM -> RUTA PAK
    // =============================================================

    private string ConvertirImagenARuta(
        string nombre)
    {
        string limpio =
            nombre.Trim();

        // ---------------------------------------------------------
        // Quitar prefijo IMAGE_REANIM_
        //
        // IMAGE_REANIM_PEASHOOTER_HEAD
        //
        // ->
        //
        // PEASHOOTER_HEAD
        // ---------------------------------------------------------

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

        // ---------------------------------------------------------
        // Normalizar separadores
        // ---------------------------------------------------------

        limpio =
            limpio.Replace(
                "\\",
                "/");

        // ---------------------------------------------------------
        // Si ya contiene REANIM/
        // ---------------------------------------------------------

        if (limpio.StartsWith(
            "REANIM/",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!limpio.EndsWith(
                ".PNG",
                StringComparison.OrdinalIgnoreCase))
            {
                limpio += ".PNG";
            }

            return limpio;
        }

        // ---------------------------------------------------------
        // Si ya termina en PNG
        // ---------------------------------------------------------

        if (limpio.EndsWith(
            ".PNG",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                "REANIM/" +
                limpio;
        }

        // ---------------------------------------------------------
        // Ruta normal
        // ---------------------------------------------------------

        return
            "REANIM/" +
            limpio +
            ".PNG";
    }


    // =============================================================
    // OBTENER NOMBRE DEL REANIM
    // =============================================================

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


    // =============================================================
    // PROPIEDADES
    // =============================================================

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


    // =============================================================
    // CONTROL MANUAL
    // =============================================================

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

        AplicarFrame(frame);
    }


    // =============================================================
    // LIMPIEZA
    // =============================================================

    private void OnDestroy()
    {
        // ---------------------------------------------------------
        // Destruir sprites y texturas creadas en runtime
        // ---------------------------------------------------------

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
    }
}