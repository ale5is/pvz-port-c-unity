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

    private readonly List<PvZReanimTrackRenderer>
        renderTracks =
        new List<PvZReanimTrackRenderer>();

    private float tiempoFrames;

    private int frameActual;

    private int frameFinal;

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

        listo =
            true;

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

    // ============================================================
    // CARGAR
    // ============================================================

    private bool CargarReanim()
    {
        if (string.IsNullOrWhiteSpace(
            rutaReanim))
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

        reanim =
            PvZReanimParser.Parse(
                datos,
                rutaReanim);

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
                "[PvZ Reanim] Atlas=" +
                atlasCreado +
                " | Sprites=" +
                atlas.Count +
                " | FPS=" +
                reanim.fps +
                " | FrameFinal=" +
                frameFinal);
        }

        return true;
    }

    // ============================================================
    // CARGAR TEXTURA
    // ============================================================

    private Texture2D CargarTexturaImagen(
        string nombreImagen)
    {
        if (
            PvZResourceManager.Instancia == null ||
            string.IsNullOrWhiteSpace(
                nombreImagen))
        {
            return null;
        }

        string ruta =
            ConvertirImagenARuta(
                nombreImagen);

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
                    "[PvZ Reanim] Imagen no encontrada: " +
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

        if (!textura.LoadImage(
            datos,
            false))
        {
            Destroy(textura);

            return null;
        }

        return textura;
    }

    // ============================================================
    // MÉTODO USADO POR TRACK RENDERER
    // ============================================================

    public Texture2D CargarTexturaParaTrack(
        string nombreImagen)
    {
        return
            CargarTexturaImagen(
                nombreImagen);
    }

    // ============================================================
    // FRAME FINAL
    // ============================================================

    private int ObtenerFrameFinal()
    {
        if (
            reanim == null ||
            reanim.tracks == null ||
            reanim.tracks.Count == 0)
        {
            return 0;
        }

        // Resodded utiliza el número de transforms
        // del primer track como duración.
        //
        // Usamos el máximo como protección
        // si algún REANIM está incompleto.

        int cantidadMaxima = 0;

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

            cantidadMaxima =
                Mathf.Max(
                    cantidadMaxima,
                    track.frames.Count);
        }

        return
            Mathf.Max(
                0,
                cantidadMaxima - 1);
    }

    // ============================================================
    // CREAR RENDERER
    // ============================================================

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

        AplicarTiempo(
            0f);
    }

    // ============================================================
    // TRACK
    // ============================================================

    private void CrearTrack(
        PvZReanimTrack track,
        int indice)
    {
        string nombre =
            string.IsNullOrWhiteSpace(
                track.name)
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

        objeto.AddComponent<MeshRenderer>();

        PvZReanimTrackRenderer rendererTrack =
            objeto.AddComponent<
                PvZReanimTrackRenderer>();

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
                " | " +
                nombre +
                " | Frames=" +
                track.frames.Count);
        }
    }

    // ============================================================
    // APLICAR TIEMPO
    // ============================================================

    private void AplicarTiempo(
        float nuevoTiempoFrames)
    {
        if (frameFinal <= 0)
        {
            tiempoFrames =
                0f;

            frameActual =
                0;

            for (
                int i = 0;
                i < renderTracks.Count;
                i++)
            {
                if (renderTracks[i] != null)
                {
                    renderTracks[i].AplicarTiempo(
                        0f,
                        escala);
                }
            }

            return;
        }

        // ========================================================
        // Igual que Resodded:
        //
        // normal loop:
        //     0 -> frameFinal
        //
        // no:
        //     frameFinal -> 0
        //
        // ========================================================

        if (hacerLoop)
        {
            float duracion =
                frameFinal;

            nuevoTiempoFrames =
                Mathf.Repeat(
                    nuevoTiempoFrames,
                    duracion);
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
                frameFinal);

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

    // ============================================================
    // UPDATE
    // ============================================================

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

    // ============================================================
    // SPRITE COMPATIBILIDAD
    // ============================================================

    public Sprite ObtenerSprite(
        string nombreImagen)
    {
        if (
            atlas == null ||
            string.IsNullOrWhiteSpace(
                nombreImagen))
        {
            return null;
        }

        Sprite sprite =
            atlas.Get(
                nombreImagen);

        if (sprite != null)
        {
            return sprite;
        }

        return
            atlas.GetIndividual(
                nombreImagen,
                CargarTexturaImagen);
    }

    // ============================================================
    // MAX IMAGE FRAME
    // ============================================================

    public int ObtenerMaxImageFrame(
        string nombreImagen)
    {
        if (
            reanim == null ||
            reanim.tracks == null ||
            string.IsNullOrWhiteSpace(
                nombreImagen))
        {
            return 0;
        }

        int maximo = 0;

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
                if (
                    frame == null ||
                    string.IsNullOrWhiteSpace(
                        frame.image))
                {
                    continue;
                }

                if (!string.Equals(
                    frame.image,
                    nombreImagen,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                maximo =
                    Mathf.Max(
                        maximo,
                        frame.imageFrame);
            }
        }

        return maximo;
    }

    // ============================================================
    // RUTA IMAGEN
    // ============================================================

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
                limpio +=
                    ".PNG";
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

    // ============================================================
    // NOMBRE
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
        tiempoFrames =
            0f;

        AplicarTiempo(
            0f);
    }

    public void IrAFrame(
        int frame)
    {
        tiempoFrames =
            frame;

        AplicarTiempo(
            tiempoFrames);
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

    // ============================================================
    // LIMPIEZA
    // ============================================================

    private void OnDestroy()
    {
        renderTracks.Clear();

        if (atlas != null)
        {
            atlas.Dispose();
            atlas = null;
        }

        if (raiz != null)
        {
            Destroy(raiz);
            raiz = null;
        }

        reanim = null;

        listo =
            false;
    }
}