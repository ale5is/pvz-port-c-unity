using System;
using System.Collections;
using UnityEngine;

public class PvZReanimParserTest : MonoBehaviour
{
    private IEnumerator Start()
    {
        // =========================================================
        // ESPERAR AL RESOURCE MANAGER
        // =========================================================

        while (PvZResourceManager.Instancia == null ||
               !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        Debug.Log(
            "[PvZ ReanimParserTest] ResourceManager listo.");

        // =========================================================
        // ARCHIVO REANIM A CARGAR
        // =========================================================

        string ruta =
            "REANIM/PEASHOOTER.REANIM";

        Debug.Log(
            $"[PvZ ReanimParserTest] Cargando: {ruta}");

        // =========================================================
        // LEER DESDE MAIN.PAK
        // =========================================================

        byte[] datos =
            PvZResourceManager.Instancia.Leer(ruta);

        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "No se pudo leer el REANIM.");

            yield break;
        }

        Debug.Log(
            $"[PvZ ReanimParserTest] " +
            $"Bytes: {datos.Length}");

        // =========================================================
        // ANALIZAR REANIM
        // =========================================================

        try
        {
            PvZReanimData reanim =
                PvZReanimParser.Parse(datos);

            // =====================================================
            // INFORMACIÓN GENERAL
            // =====================================================

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                $"FPS: {reanim.fps}");

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                $"Tracks: {reanim.tracks.Count}");

            // =====================================================
            // MOSTRAR TRACKS
            // =====================================================

            foreach (PvZReanimTrack track in reanim.tracks)
            {
                if (track == null)
                    continue;

                Debug.Log(
                    "[PvZ REANIM] " +
                    $"Track: {track.name} | " +
                    $"Frames: {track.frames.Count}");

                // =================================================
                // MOSTRAR SOLO FRAMES QUE TENGAN INFORMACIÓN
                // =================================================

                foreach (PvZReanimFrame frame in track.frames)
                {
                    if (frame == null)
                        continue;

                    if (!frame.tieneTransformacion)
                        continue;

                    Debug.Log(
                        "[PvZ REANIM FRAME] " +
                        $"Track={track.name} | " +
                        $"Frame={frame.f} | " +
                        $"X={frame.x} | " +
                        $"Y={frame.y} | " +
                        $"SX={frame.sx} | " +
                        $"SY={frame.sy} | " +
                        $"Image={frame.image}");
                }
            }

            // =====================================================
            // RESULTADO
            // =====================================================

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                "========================================");

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                "¡REANIM interpretado correctamente!");

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                $"FPS = {reanim.fps}");

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                $"Tracks = {reanim.tracks.Count}");

            Debug.Log(
                "[PvZ ReanimParserTest] " +
                "========================================");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimParserTest] " +
                "Error analizando REANIM:\n" +
                e);
        }
    }
}