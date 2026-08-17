using System.Collections;
using UnityEngine;

public class PvZReanimTest : MonoBehaviour
{
    [Header("Reanimación a cargar")]
    public string nombreReanim = "REANIM/PEASHOOTER.REANIM";

    private IEnumerator Start()
    {
        // Esperar a que exista el ResourceManager
        while (PvZResourceManager.Instancia == null)
            yield return null;

        // Esperar a que termine de cargar main.pak
        while (!PvZResourceManager.Instancia.EstaListo)
            yield return null;

        Debug.Log(
            "[PvZ ReanimTest] ResourceManager listo."
        );

        CargarReanim();
    }

    private void CargarReanim()
    {
        Debug.Log(
            $"[PvZ ReanimTest] Buscando: {nombreReanim}"
        );

        PvZPakFile archivo;

        bool encontrado =
            PvZResourceManager.Instancia.ObtenerArchivo(
                nombreReanim,
                out archivo
            );

        if (!encontrado || archivo == null)
        {
            Debug.LogError(
                $"[PvZ ReanimTest] No se encontró: {nombreReanim}"
            );

            return;
        }

        Debug.Log(
            $"[PvZ ReanimTest] ¡REANIM encontrado! " +
            $"Nombre: {archivo.Name} | " +
            $"Tamaño: {archivo.Size}"
        );
    }
}