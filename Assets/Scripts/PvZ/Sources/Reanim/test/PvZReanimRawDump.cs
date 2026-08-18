using System;
using System.Text;
using UnityEngine;

public class PvZReanimRawDump : MonoBehaviour
{
    [SerializeField]
    private string rutaReanim =
        "REANIM/PEASHOOTER.REANIM";

    private void Start()
    {
        Invoke(nameof(Analizar), 1f);
    }

    private void Analizar()
    {
        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError("[PvZ RawDump] ResourceManager NULL.");
            return;
        }

        if (!PvZResourceManager.Instancia.EstaListo)
        {
            Debug.LogError("[PvZ RawDump] ResourceManager no está listo.");
            return;
        }

        byte[] datos =
            PvZResourceManager.Instancia.Leer(rutaReanim);

        if (datos == null || datos.Length == 0)
        {
            Debug.LogError(
                "[PvZ RawDump] No se pudo leer: " +
                rutaReanim);

            return;
        }

        Debug.Log(
            "[PvZ RawDump] Bytes: " +
            datos.Length);

        // =====================================================
        // PRIMEROS BYTES
        // =====================================================

        int cantidad =
            Mathf.Min(
                datos.Length,
                512);

        StringBuilder hex =
            new StringBuilder();

        for (int i = 0; i < cantidad; i++)
        {
            hex.Append(
                datos[i].ToString("X2"));

            hex.Append(' ');

            if ((i + 1) % 16 == 0)
                hex.AppendLine();
        }

        Debug.Log(
            "[PvZ RawDump] HEX:\n" +
            hex);

        // =====================================================
        // TEXTO ASCII
        // =====================================================

        string ascii =
            Encoding.ASCII.GetString(
                datos);

        int inicio =
            ascii.IndexOf("<");

        if (inicio >= 0)
        {
            int cantidadTexto =
                Mathf.Min(
                    8000,
                    ascii.Length - inicio);

            string texto =
                ascii.Substring(
                    inicio,
                    cantidadTexto);

            Debug.Log(
                "[PvZ RawDump] TEXTO DESDE '<':\n" +
                texto);
        }
        else
        {
            Debug.LogWarning(
                "[PvZ RawDump] No se encontró '<'.");

            Debug.Log(
                "[PvZ RawDump] ASCII:\n" +
                ascii.Substring(
                    0,
                    Mathf.Min(
                        8000,
                        ascii.Length)));
        }
    }
}