using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PvZReanimAssetTest : MonoBehaviour
{
    private IEnumerator Start()
    {
        // =========================================================
        // ESPERAR RESOURCE MANAGER
        // =========================================================

        while (PvZResourceManager.Instancia == null ||
               !PvZResourceManager.Instancia.EstaListo)
        {
            yield return null;
        }

        Debug.Log(
            "[PvZ ReanimAssetTest] ResourceManager listo.");

        // =========================================================
        // CARGAR REANIM
        // =========================================================

        string ruta = "REANIM/PEASHOOTER.REANIM";

        Debug.Log(
            $"[PvZ ReanimAssetTest] Cargando: {ruta}");

        byte[] datos =
            PvZResourceManager.Instancia.Leer(ruta);

        if (datos == null)
        {
            Debug.LogError(
                "[PvZ ReanimAssetTest] " +
                "No se pudo leer el REANIM.");

            yield break;
        }

        PvZReanimData reanim;

        try
        {
            reanim = PvZReanimParser.Parse(datos);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimAssetTest] Error parseando REANIM:\n" +
                e);

            yield break;
        }

        Debug.Log(
            $"[PvZ ReanimAssetTest] REANIM cargado. " +
            $"Tracks: {reanim.tracks.Count}");

        // =========================================================
        // OBTENER TODAS LAS IMÁGENES USADAS
        // =========================================================

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
                if (frame == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(frame.image))
                {
                    continue;
                }

                imagenes.Add(frame.image.Trim());
            }
        }

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] " +
            $"Imágenes únicas encontradas: {imagenes.Count}");

        Debug.Log("================================================");

        foreach (string imagen in imagenes)
        {
            Debug.Log(
                "[PvZ REANIM IMAGE] " +
                imagen);
        }

        // =========================================================
        // OBTENER TODOS LOS ARCHIVOS DEL PAK
        // =========================================================

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] " +
            "Obteniendo todos los archivos internos del PAK...");

        IEnumerable<PvZPakFile> archivos =
            PvZResourceManager.Instancia
                .ObtenerTodosLosArchivos();

        if (archivos == null)
        {
            Debug.LogError(
                "[PvZ ReanimAssetTest] " +
                "ObtenerTodosLosArchivos() devolvió null.");

            yield break;
        }

        List<PvZPakFile> listaArchivos =
            new List<PvZPakFile>(archivos);

        Debug.Log(
            "[PvZ ReanimAssetTest] Archivos obtenidos: " +
            listaArchivos.Count);

        // =========================================================
        // MOSTRAR ARCHIVOS RELACIONADOS CON PEASHOOTER
        // =========================================================

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] " +
            "Buscando archivos relacionados con PEASHOOTER...");

        int encontradosPeashooter = 0;

        foreach (PvZPakFile archivo in listaArchivos)
        {
            if (archivo == null)
            {
                continue;
            }

            string rutaArchivo = archivo.ToString();

            if (rutaArchivo.IndexOf(
                    "PEASHOOTER",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log(
                    "[PvZ PAK PEASHOOTER] " +
                    rutaArchivo);

                encontradosPeashooter++;
            }
        }

        Debug.Log(
            "[PvZ ReanimAssetTest] " +
            $"Archivos PEASHOOTER encontrados: " +
            encontradosPeashooter);

        // =========================================================
        // BUSCAR LAS IMÁGENES
        // =========================================================

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] " +
            "Convirtiendo nombres REANIM -> rutas del PAK...");

        int encontradas = 0;
        int faltantes = 0;

        foreach (string imagen in imagenes)
        {
            string rutaPak = ConvertirImagenARutaPak(imagen);

            Debug.Log(
                "[PvZ REANIM MAP] " +
                imagen +
                " -> " +
                rutaPak);

            PvZPakFile encontrado =
                BuscarArchivo(
                    listaArchivos,
                    rutaPak);

            if (encontrado != null)
            {
                Debug.Log(
                    "[PvZ ASSET ENCONTRADO] " +
                    imagen +
                    " -> " +
                    rutaPak +
                    " -> " +
                    encontrado);

                encontradas++;
            }
            else
            {
                Debug.LogWarning(
                    "[PvZ ASSET FALTA] " +
                    imagen +
                    " -> " +
                    rutaPak);

                faltantes++;
            }
        }

        // =========================================================
        // RESULTADO
        // =========================================================

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] RESULTADO");

        Debug.Log(
            $"Imágenes únicas: {imagenes.Count}");

        Debug.Log(
            $"Encontradas: {encontradas}");

        Debug.Log(
            $"Faltantes: {faltantes}");

        Debug.Log("================================================");

        if (faltantes == 0)
        {
            Debug.Log(
                "[PvZ ReanimAssetTest] " +
                "¡TODAS las imágenes del REANIM fueron localizadas!");
        }
        else
        {
            Debug.LogWarning(
                "[PvZ ReanimAssetTest] " +
                "Todavía hay imágenes que no fueron localizadas.");
        }

        Debug.Log("================================================");

        Debug.Log(
            "[PvZ ReanimAssetTest] TEST TERMINADO.");
    }

    // =============================================================
    // CONVERTIR NOMBRE DEL REANIM A RUTA DEL PAK
    // =============================================================

    private string ConvertirImagenARutaPak(string imagen)
    {
        if (string.IsNullOrWhiteSpace(imagen))
        {
            return null;
        }

        string nombre = imagen.Trim();

        // ---------------------------------------------------------
        // Ejemplo:
        //
        // IMAGE_REANIM_PEASHOOTER_HEAD
        //
        // se convierte en:
        //
        // REANIM/PEASHOOTER_HEAD.PNG
        // ---------------------------------------------------------

        const string prefijo = "IMAGE_REANIM_";

        if (nombre.StartsWith(
                prefijo,
                StringComparison.OrdinalIgnoreCase))
        {
            nombre = nombre.Substring(prefijo.Length);
        }

        // El nombre ya es el nombre interno de la imagen.
        nombre = nombre.ToUpperInvariant();

        return "REANIM/" + nombre + ".PNG";
    }

    // =============================================================
    // BUSCAR ARCHIVO EN EL PAK
    // =============================================================

    private PvZPakFile BuscarArchivo(
        List<PvZPakFile> archivos,
        string rutaBuscada)
    {
        if (archivos == null ||
            string.IsNullOrWhiteSpace(rutaBuscada))
        {
            return null;
        }

        string rutaNormalizada =
            NormalizarRuta(rutaBuscada);

        foreach (PvZPakFile archivo in archivos)
        {
            if (archivo == null)
            {
                continue;
            }

            string texto = archivo.ToString();

            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            string textoNormalizado =
                NormalizarRuta(texto);

            // -----------------------------------------------------
            // COINCIDENCIA EXACTA
            // -----------------------------------------------------

            if (textoNormalizado.Equals(
                    rutaNormalizada,
                    StringComparison.OrdinalIgnoreCase))
            {
                return archivo;
            }

            // -----------------------------------------------------
            // COINCIDENCIA POR RUTA
            // -----------------------------------------------------

            if (textoNormalizado.IndexOf(
                    rutaNormalizada,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return archivo;
            }
        }

        return null;
    }

    // =============================================================
    // NORMALIZAR RUTA
    // =============================================================

    private string NormalizarRuta(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return string.Empty;
        }

        string resultado = ruta.Trim();

        resultado = resultado.Replace('\\', '/');

        while (resultado.Contains("//"))
        {
            resultado = resultado.Replace("//", "/");
        }

        return resultado;
    }
}