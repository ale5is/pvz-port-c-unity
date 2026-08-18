using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PvZReanimParserTest : MonoBehaviour
{
    [Header("REANIM")]
    [SerializeField]
    private string rutaReanim = "REANIM/PEASHOOTER.REANIM";

    [Header("Debug")]
    [SerializeField]
    private bool mostrarTodosLosFrames = true;

    [SerializeField]
    private int maxFramesPorTrack = 20;

    private IEnumerator Start()
    {
        while (
            PvZResourceManager.Instancia == null ||
            !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        Debug.Log(
            "[PvZ ReanimParserTest] ResourceManager listo.");

        byte[] datos =
            PvZResourceManager.Instancia.Leer(rutaReanim);

        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] No se pudo leer el REANIM:\n" +
                rutaReanim);
            yield break;
        }

        Debug.Log(
            "[PvZ ReanimParserTest] Bytes: " +
            datos.Length);

        PvZReanimData reanim;

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
                "[PvZ ReanimParserTest] Error analizando REANIM:\n" +
                e);
            yield break;
        }

        if (reanim == null)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] El parser devolvió NULL.");
            yield break;
        }

        if (reanim.tracks == null)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] El REANIM no contiene tracks.");
            yield break;
        }

        int cantidadTracks = reanim.tracks.Count;
        int cantidadFramesTotal = 0;
        int cantidadFramesMaxima = 0;

        foreach (PvZReanimTrack track in reanim.tracks)
        {
            if (track == null || track.frames == null)
                continue;

            cantidadFramesTotal += track.frames.Count;

            cantidadFramesMaxima =
                Mathf.Max(
                    cantidadFramesMaxima,
                    track.frames.Count);
        }

        Debug.Log(
            "[PvZ ReanimParserTest] ========================================");

        Debug.Log(
            "[PvZ ReanimParserTest] REANIM PARSEADO");

        Debug.Log(
            "[PvZ ReanimParserTest] Ruta: " +
            rutaReanim);

        Debug.Log(
            "[PvZ ReanimParserTest] FPS: " +
            reanim.fps);

        Debug.Log(
            "[PvZ ReanimParserTest] Tracks: " +
            cantidadTracks);

        Debug.Log(
            "[PvZ ReanimParserTest] Frames totales: " +
            cantidadFramesTotal);

        Debug.Log(
            "[PvZ ReanimParserTest] Frames máximos por track: " +
            cantidadFramesMaxima);

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
                    "[PvZ REANIM TEST] Track " +
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
                "Índice=" +
                indiceTrack +
                " | Nombre=" +
                track.name +
                " | Frames=" +
                cantidadFrames);

            if (cantidadFrames == 0)
                continue;

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
                        "[PvZ REANIM FRAME] Frame " +
                        indiceFrame +
                        " es NULL.");
                    continue;
                }

                Debug.Log(
                    "[PvZ REANIM FRAME] " +
                    "Track=" +
                    track.name +
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
                    " | Alpha=" +
                    frame.alpha +
                    " | Image=" +
                    frame.image +
                    " | Transform=" +
                    frame.tieneTransformacion);
            }

            if (cantidadAMostrar < cantidadFrames)
            {
                Debug.Log(
                    "[PvZ REANIM TRACK] Se muestran " +
                    cantidadAMostrar +
                    " de " +
                    cantidadFrames +
                    " frames.");
            }
        }

        int imagenesUnicas =
            ContarImagenesUnicas(reanim);

        Debug.Log(
            "[PvZ ReanimParserTest] Imágenes únicas: " +
            imagenesUnicas);

        Debug.Log(
            "[PvZ ReanimParserTest] ========================================");

        Debug.Log(
            "[PvZ ReanimParserTest] " +
            "¡REANIM INTERPRETADO CORRECTAMENTE!");

        Debug.Log(
            "[PvZ ReanimParserTest] FPS=" +
            reanim.fps +
            " | Tracks=" +
            cantidadTracks +
            " | Frames=" +
            cantidadFramesTotal +
            " | Imágenes=" +
            imagenesUnicas);

        Debug.Log(
            "[PvZ ReanimParserTest] ========================================");
    }

    private int ContarImagenesUnicas(
        PvZReanimData reanim)
    {
        if (reanim == null ||
            reanim.tracks == null)
        {
            return 0;
        }

        HashSet<string> imagenes =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (PvZReanimTrack track in reanim.tracks)
        {
            if (track == null ||
                track.frames == null)
            {
                continue;
            }

            foreach (PvZReanimFrame frame in track.frames)
            {
                if (frame == null ||
                    string.IsNullOrWhiteSpace(frame.image))
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