using UnityEngine;

public class PvZPakDebugger : MonoBehaviour
{
    public string buscar = "";

    [ContextMenu("Buscar recursos")]
    public void ListarArchivos()
    {
        if (PvZResourceManager.Instancia == null)
        {
            Debug.LogError("[PvZ Debugger] No existe PvZResourceManager.");
            return;
        }

        int encontrados = 0;

        foreach (PvZPakFile archivo in PvZResourceManager.Instancia.ObtenerTodosLosArchivos())
        {
            string nombre = archivo.Name.ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(buscar) &&
                !nombre.Contains(buscar.ToUpperInvariant()))
            {
                continue;
            }

            Debug.Log(
                $"[PvZ PAK] {archivo.Name} | Size: {archivo.Size}"
            );

            encontrados++;
        }

        Debug.Log(
            $"[PvZ Debugger] Recursos encontrados: {encontrados}"
        );
    }
}