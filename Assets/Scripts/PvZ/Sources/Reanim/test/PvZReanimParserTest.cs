using System;
using System.Collections;
using UnityEngine;

public class PvZReanimParserTest : MonoBehaviour
{
    // ============================================================
    // CONFIGURACIÓN
    // ============================================================

    [Header("REANIM")]
    [SerializeField]
    private string rutaReanim =
        "REANIM/PEASHOOTER.REANIM";

    [Header("Debug")]
    [SerializeField]
    private bool mostrarTodosLosFrames = true;

    [SerializeField]
    private int maxFramesPorTrack = 20;

    // ============================================================
    // START
    // ============================================================

    private IEnumerator Start()
    {
        // ========================================================
        // ESPERAR RESOURCE MANAGER
        // ========================================================

        while (
            PvZResourceManager.Instancia == null ||
            !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "ResourceManager listo.");

        // ========================================================
        // CARGAR REANIM
        // ========================================================

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "========================================");

        Debug.Log(
            "[PvZ ReanimParserTest] Cargando: " +
            rutaReanim);

        byte[] datos =
            PvZResourceManager.Instancia.Leer(
                rutaReanim);

        if (datos == null ||
            datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "No se pudo leer el REANIM:\n" +
                rutaReanim);

            yield break;
        }

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Bytes: " +
            datos.Length);

        // ========================================================
        // PARSEAR
        // ========================================================

        PvZReanimData reanim = null;

        try
        {
            reanim =
                PvZReanimParser.Parse(
                    datos,
                    rutaReanim);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "Error analizando REANIM:\n" +
                e);

            yield break;
        }

        // ========================================================
        // VALIDAR
        // ========================================================

        if (reanim == null)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "El parser devolvió NULL.");

            yield break;
        }

        if (reanim.tracks == null)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "El REANIM no contiene lista de tracks.");

            yield break;
        }

        // ========================================================
        // INFORMACIÓN GENERAL
        // ========================================================

        int cantidadTracks =
            reanim.tracks.Count;

        int cantidadFramesTotal = 0;

        int cantidadFramesMaxima = 0;

        foreach (
            PvZReanimTrack track
            in reanim.tracks)
        {
            if (track == null ||
                track.frames == null)
            {
                continue;
            }

            cantidadFramesTotal +=
                track.frames.Count;

            cantidadFramesMaxima =
                Mathf.Max(
                    cantidadFramesMaxima,
                    track.frames.Count);
        }

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "========================================");

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "REANIM PARSEADO");

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Ruta: " +
            rutaReanim);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "FPS: " +
            reanim.fps);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Tracks: " +
            cantidadTracks);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Frames totales: " +
            cantidadFramesTotal);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Frames máximos por track: " +
            cantidadFramesMaxima);

        // ========================================================
        // MOSTRAR TRACKS
        // ========================================================

        for (
            int indiceTrack = 0;
            indiceTrack < reanim.tracks.Count;
            indiceTrack++)
        {
            PvZReanimTrack track =
                reanim.tracks[indiceTrack];

            if (track == null)
            {
                Debug.LogWarning(
                    "[PvZ REANIM TEST] " +
                    "Track " +
                    indiceTrack +
                    " es NULL.");

                continue;
            }

            int cantidadFrames =
                track.frames != null
                    ? track.frames.Count
                    : 0;

            Debug.Log(
                "[PvZ REANIM TRACK] " +
                "================================");

            Debug.Log(
                "[PvZ REANIM TRACK] " +
                "Índice: " +
                indiceTrack);

            Debug.Log(
                "[PvZ REANIM TRACK] " +
                "Nombre: " +
                track.name);

            Debug.Log(
                "[PvZ REANIM TRACK] " +
                "Frames: " +
                cantidadFrames);

            // ----------------------------------------------------
            // SIN FRAMES
            // ----------------------------------------------------

            if (cantidadFrames == 0)
            {
                Debug.LogWarning(
                    "[PvZ REANIM TRACK] " +
                    "Este track no tiene frames.");

                continue;
            }

            // ----------------------------------------------------
            // FRAMES
            // ----------------------------------------------------

            int cantidadAMostrar =
                mostrarTodosLosFrames
                    ? cantidadFrames
                    : Mathf.Min(
                        cantidadFrames,
                        maxFramesPorTrack);

            for (
                int indiceFrame = 0;
                indiceFrame < cantidadAMostrar;
                indiceFrame++)
            {
                PvZReanimFrame frame =
                    track.frames[indiceFrame];

                if (frame == null)
                {
                    Debug.LogWarning(
                        "[PvZ REANIM FRAME] " +
                        "Frame " +
                        indiceFrame +
                        " es NULL.");

                    continue;
                }

                // =================================================
                // INFORMACIÓN DEL FRAME
                // =================================================

                Debug.Log(
                    "[PvZ REANIM FRAME] " +
                    "Track=" +
                    indiceTrack +
                    " (" +
                    track.name +
                    ")" +
                    " | Frame=" +
                    indiceFrame +
                    " | X=" +
                    frame.x +
                    " | Y=" +
                    frame.y +
                    " | SX=" +
                    frame.sx +
                    " | SY=" +
                    frame.sy +
                    " | Rot=" +
                    //frame.f +
                    " | Image=" +
                    frame.image +
                    " | Transform=" +
                    frame.tieneTransformacion);
            }

            // ----------------------------------------------------
            // AVISO SI SE OMITIERON FRAMES
            // ----------------------------------------------------

            if (cantidadAMostrar < cantidadFrames)
            {
                Debug.Log(
                    "[PvZ REANIM TRACK] " +
                    "Se muestran " +
                    cantidadAMostrar +
                    " de " +
                    cantidadFrames +
                    " frames.");
            }
        }

        // ========================================================
        // BUSCAR IMÁGENES ÚNICAS
        // ========================================================

        int imagenesUnicas =
            ContarImagenesUnicas(reanim);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Imágenes únicas utilizadas: " +
            imagenesUnicas);

        // ========================================================
        // RESULTADO FINAL
        // ========================================================

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "========================================");

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "¡REANIM INTERPRETADO CORRECTAMENTE!");

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "FPS = " +
            reanim.fps);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Tracks = " +
            cantidadTracks);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Frames máximos = " +
            cantidadFramesMaxima);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "Imágenes únicas = " +
            imagenesUnicas);

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "========================================");
    }

    // ============================================================
    // CONTAR IMÁGENES ÚNICAS
    // ============================================================

    private int ContarImagenesUnicas(
        PvZReanimData reanim)
    {
        if (reanim == null ||
            reanim.tracks == null)
        {
            return 0;
        }

        System.Collections.Generic.HashSet<string>
            imagenes =
                new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            PvZReanimTrack track
            in reanim.tracks)
        {
            if (track == null ||
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

                if (string.IsNullOrWhiteSpace(
                    frame.image))
                {
                    continue;
                }

                imagenes.Add(
                    frame.image.Trim());
            }
        }

        return imagenes.Count;
    }
}