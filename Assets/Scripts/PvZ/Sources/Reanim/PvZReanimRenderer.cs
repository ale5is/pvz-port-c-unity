using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PvZReanimRenderer : MonoBehaviour
{
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

    [Header("Loop")]
    [SerializeField]
    private bool hacerLoop = true;

    [Header("Debug")]
    [SerializeField]
    private bool mostrarDebug = true;

    private PvZReanimData reanim;

    private UnityEngine.GameObject raiz;

    private PvZReanimAtlas atlas;

    private readonly List<PvZReanimTrackRenderer> renderTracks =
        new List<PvZReanimTrackRenderer>();

    private float tiempoFrames;

    private int frameActual;

    private int frameFinal;

    private bool listo;

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
                rutaReanim +
                " | FPS=" +
                FPS +
                " | FrameFinal=" +
                frameFinal +
                " | Tracks=" +
                renderTracks.Count);
        }
    }

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

        if (
            datos == null ||
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

        frameFinal =
            ObtenerFrameFinal();

        atlas =
            new PvZReanimAtlas();

        bool atlasCreado =
            atlas.Build(
                reanim,
                CargarTexturaImagen);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] Atlas creado: " +
                atlasCreado +
                " | Sprites: " +
                atlas.Count);
        }

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim] FPS=" +
                reanim.fps +
                " | Tracks=" +
                (
                    reanim.tracks != null
                        ? reanim.tracks.Count
                        : 0
                ) +
                " | FrameFinal=" +
                frameFinal);
        }

        return true;
    }

    private Texture2D CargarTexturaImagen(
        string nombreImagen)
    {
        if (
            PvZResourceManager.Instancia == null ||
            string.IsNullOrWhiteSpace(nombreImagen))
        {
            return null;
        }

        string ruta =
            ConvertirImagenARuta(
                nombreImagen);

        if (mostrarDebug)
        {
            Debug.Log(
                "[PvZ Reanim Atlas] Imagen: " +
                nombreImagen +
                " -> " +
                ruta);
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                ruta);

        if (
            datos == null ||
            datos.Length == 0)
        {
            if (mostrarDebug)
            {
                Debug.LogWarning(
                    "[PvZ Reanim Atlas] " +
                    "Imagen no encontrada: " +
                    ruta);
            }

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

        textura.filterMode =
            FilterMode.Point;

        textura.wrapMode =
            TextureWrapMode.Clamp;

        bool cargada =
            textura.LoadImage(
                datos,
                false);

        if (!cargada)
        {
            Debug.LogError(
                "[PvZ Reanim Atlas] " +
                "No se pudo cargar: " +
                nombreImagen);

            Destroy(textura);

            return null;
        }

        return textura;
    }

    private int ObtenerFrameFinal()
    {
        int maximo = 0;

        if (
            reanim == null ||
            reanim.tracks == null)
        {
            return 0;
        }

        foreach (
            PvZReanimTrack track
            in reanim.tracks)
        {
            if (
                track == null ||
                track.frames == null)
            {
                continue;
            }

            foreach (
                PvZReanimFrame frame
                in track.frames)
            {
                if (frame == null)
                {
                    continue;
                }

                if (
                    frame.frameNumber >
                    maximo)
                {
                    maximo =
                        frame.frameNumber;
                }
            }
        }

        return maximo;
    }

    private void CrearRenderer()
    {
        if (raiz != null)
        {
            Destroy(raiz);

            raiz = null;
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

        if (
            reanim == null ||
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

        AplicarTiempo(0f);
    }

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

        objeto.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer =
            objeto.AddComponent<MeshRenderer>();

        meshRenderer.sortingOrder =
            indice;

        PvZReanimTrackRenderer rendererTrack =
            objeto.AddComponent<PvZReanimTrackRenderer>();

        rendererTrack.Inicializar(
            this,
            track,
            null,
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
                " | Frames=" +
                (
                    track.frames != null
                        ? track.frames.Count
                        : 0
                ) +
                " | FrameInicial=" +
                ObtenerPrimerFrame(track) +
                " | FrameFinal=" +
                ObtenerUltimoFrame(track));
        }
    }

    private int ObtenerPrimerFrame(
        PvZReanimTrack track)
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return 0;
        }

        int minimo =
            int.MaxValue;

        foreach (
            PvZReanimFrame frame
            in track.frames)
        {
            if (frame == null)
            {
                continue;
            }

            if (
                frame.frameNumber <
                minimo)
            {
                minimo =
                    frame.frameNumber;
            }
        }

        return
            minimo == int.MaxValue
                ? 0
                : minimo;
    }

    private int ObtenerUltimoFrame(
        PvZReanimTrack track)
    {
        if (
            track == null ||
            track.frames == null ||
            track.frames.Count == 0)
        {
            return 0;
        }

        int maximo = 0;

        foreach (
            PvZReanimFrame frame
            in track.frames)
        {
            if (frame == null)
            {
                continue;
            }

            if (
                frame.frameNumber >
                maximo)
            {
                maximo =
                    frame.frameNumber;
            }
        }

        return maximo;
    }

    private void AplicarTiempo(
        float nuevoTiempoFrames)
    {
        if (frameFinal < 0)
        {
            return;
        }

        if (hacerLoop)
        {
            float duracion =
                frameFinal + 1f;

            if (duracion > 0f)
            {
                nuevoTiempoFrames =
                    nuevoTiempoFrames %
                    duracion;

                if (
                    nuevoTiempoFrames <
                    0f)
                {
                    nuevoTiempoFrames +=
                        duracion;
                }
            }
        }
        else
        {
            nuevoTiempoFrames =
                Mathf.Clamp(
                    nuevoTiempoFrames,
                    0f,
                    frameFinal);
        }

        tiempoFrames =
            nuevoTiempoFrames;

        frameActual =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    tiempoFrames),
                0,
                Mathf.Max(
                    0,
                    frameFinal));

        for (
            int i = 0;
            i < renderTracks.Count;
            i++)
        {
            PvZReanimTrackRenderer track =
                renderTracks[i];

            if (track == null)
            {
                continue;
            }

            track.AplicarTiempo(
                tiempoFrames,
                escala);
        }
    }

    private void Update()
    {
        if (
            !listo ||
            !reproducirAutomaticamente ||
            reanim == null ||
            frameFinal <= 0)
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

        tiempoFrames +=
            Time.deltaTime *
            velocidad;

        AplicarTiempo(
            tiempoFrames);
    }

    public Sprite ObtenerSprite(
        string nombreImagen)
    {
        if (
            string.IsNullOrWhiteSpace(
                nombreImagen))
        {
            return null;
        }

        if (atlas == null)
        {
            Debug.LogError(
                "[PvZ Reanim] Atlas no inicializado.");

            return null;
        }

        nombreImagen =
            nombreImagen.Trim();

        Sprite sprite =
            atlas.Get(
                nombreImagen);

        if (sprite != null)
        {
            return sprite;
        }

        sprite =
            atlas.GetIndividual(
                nombreImagen,
                CargarTexturaImagen);

        if (sprite == null)
        {
            if (mostrarDebug)
            {
                Debug.LogWarning(
                    "[PvZ Reanim] " +
                    "Imagen no encontrada: " +
                    nombreImagen);
            }

            return null;
        }

        return sprite;
    }

    private string ConvertirImagenARuta(
        string nombre)
    {
        string limpio =
            nombre.Trim();

        const string prefijo =
            "IMAGE_REANIM_";

        if (
            limpio.StartsWith(
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

        if (
            limpio.StartsWith(
                "REANIM/",
                StringComparison.OrdinalIgnoreCase))
        {
            if (
                !limpio.EndsWith(
                    ".PNG",
                    StringComparison.OrdinalIgnoreCase))
            {
                limpio += ".PNG";
            }

            return limpio;
        }

        if (
            limpio.EndsWith(
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
        tiempoFrames = 0f;

        AplicarTiempo(0f);
    }

    public void IrAFrame(
        int frame)
    {
        tiempoFrames =
            frame;

        AplicarTiempo(
            tiempoFrames);
    }

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
            return frameFinal + 1;
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

    public PvZReanimAtlas Atlas
    {
        get
        {
            return atlas;
        }
    }

    private void OnDestroy()
    {
        renderTracks.Clear();

        atlas = null;
        reanim = null;

        if (raiz != null)
        {
            Destroy(raiz);
            raiz = null;
        }

        listo = false;
    }
}