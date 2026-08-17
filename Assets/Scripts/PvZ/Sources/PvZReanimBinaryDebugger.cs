using System;
using UnityEngine;

public class PvZReanimBinaryDebugger : MonoBehaviour
{
    [Header("Archivo REANIM")]
    public string archivoReanim = "REANIM/PEASHOOTER.REANIM";

    [Header("Depuración")]
    [Tooltip("Cantidad máxima de caracteres XML que se mostrarán en la consola.")]
    public int caracteresXML = 5000;

    private System.Collections.IEnumerator Start()
    {
        // Esperamos un frame para permitir que PvZResourceManager
        // termine su inicialización.
        yield return null;

        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ ReanimBinaryDebugger] " +
                "PvZResourceManager no está disponible."
            );

            yield break;
        }

        if (!PvZResourceManager.Instancia.EstaListo)
        {
            Debug.LogError(
                "[PvZ ReanimBinaryDebugger] " +
                "PvZResourceManager todavía no está listo."
            );

            yield break;
        }

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "ResourceManager listo."
        );

        AnalizarReanim();
    }

    private void AnalizarReanim()
    {
        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Buscando: " + archivoReanim
        );

        // -----------------------------------------
        // BUSCAR EL ARCHIVO EN EL PAK
        // -----------------------------------------

        if (!PvZResourceManager.Instancia.ObtenerArchivo(
                archivoReanim,
                out PvZPakFile archivo))
        {
            Debug.LogError(
                "[PvZ ReanimBinaryDebugger] " +
                "REANIM no encontrado: " +
                archivoReanim
            );

            return;
        }

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "REANIM encontrado."
        );

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Nombre: " + archivo.Name
        );

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Offset: " + archivo.Offset
        );

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Tamaño: " + archivo.Size
        );

        // -----------------------------------------
        // LEER LOS BYTES REALES
        // -----------------------------------------

        byte[] datos = PvZResourceManager.Instancia.Leer(
            archivoReanim
        );

        if (datos == null)
        {
            Debug.LogError(
                "[PvZ ReanimBinaryDebugger] " +
                "No se pudieron leer los datos del REANIM."
            );

            return;
        }

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Bytes leídos: " + datos.Length
        );

        // -----------------------------------------
        // MOSTRAR CABECERA HEXADECIMAL
        // -----------------------------------------

        MostrarCabecera(datos);

        // -----------------------------------------
        // MOSTRAR XML
        // -----------------------------------------

        MostrarXML(datos);

        // -----------------------------------------
        // BUSCAR TEXTO LEGIBLE
        // -----------------------------------------

        BuscarTexto(datos);
    }

    // ============================================================
    // CABECERA HEXADECIMAL
    // ============================================================

    private void MostrarCabecera(byte[] datos)
    {
        int cantidad = Mathf.Min(64, datos.Length);

        string hexadecimal = "";

        for (int i = 0; i < cantidad; i++)
        {
            hexadecimal += datos[i].ToString("X2") + " ";

            if ((i + 1) % 16 == 0)
                hexadecimal += "\n";
        }

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Primeros bytes:\n" +
            hexadecimal
        );
    }

    // ============================================================
    // MOSTRAR XML
    // ============================================================

    private void MostrarXML(byte[] datos)
    {
        try
        {
            string xml = System.Text.Encoding.UTF8.GetString(datos);

            if (string.IsNullOrEmpty(xml))
            {
                Debug.LogWarning(
                    "[PvZ ReanimBinaryDebugger] " +
                    "El REANIM no contiene texto."
                );

                return;
            }

            int cantidad = Mathf.Min(
                caracteresXML,
                xml.Length
            );

            string fragmento = xml.Substring(
                0,
                cantidad
            );

            Debug.Log(
                "[PvZ REANIM XML]\n" +
                fragmento
            );

            Debug.Log(
                "[PvZ ReanimBinaryDebugger] " +
                "Caracteres XML totales: " +
                xml.Length
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[PvZ ReanimBinaryDebugger] " +
                "Error leyendo XML:\n" +
                e
            );
        }
    }

    // ============================================================
    // BUSCAR TEXTO LEGIBLE
    // ============================================================

    private void BuscarTexto(byte[] datos)
    {
        int encontrados = 0;

        string textoActual = "";

        for (int i = 0; i < datos.Length; i++)
        {
            byte b = datos[i];

            bool imprimible =
                b >= 32 &&
                b <= 126;

            if (imprimible)
            {
                textoActual += (char)b;
            }
            else
            {
                if (textoActual.Length >= 4)
                {
                    Debug.Log(
                        "[PvZ REANIM TEXTO] " +
                        textoActual
                    );

                    encontrados++;
                }

                textoActual = "";
            }
        }

        // Procesar el último texto si terminó
        // sin un carácter no imprimible.
        if (textoActual.Length >= 4)
        {
            Debug.Log(
                "[PvZ REANIM TEXTO] " +
                textoActual
            );

            encontrados++;
        }

        Debug.Log(
            "[PvZ ReanimBinaryDebugger] " +
            "Cadenas encontradas: " +
            encontrados
        );
    }
}