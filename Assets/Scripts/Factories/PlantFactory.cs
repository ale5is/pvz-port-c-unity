using UnityEngine;

public class PlantFactory : MonoBehaviour
{
    public static PlantFactory Instancia { get; private set; }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    public Plant CrearPlanta(
        PlantData datos,
        int fila,
        int columna)
    {
        if (datos == null)
        {
            Debug.LogError("[PvZ] PlantData es null.");
            return null;
        }

        if (datos.prefab == null)
        {
            Debug.LogError(
                "[PvZ] El PlantData '" +
                datos.nombre +
                "' no tiene prefab."
            );

            return null;
        }

        if (Board.Instancia == null)
        {
            Debug.LogError("[PvZ] No existe Board.");
            return null;
        }

        Cell cell =
            Board.Instancia.ObtenerCelda(
                fila,
                columna
            );

        if (cell == null)
        {
            Debug.LogWarning(
                "[PvZ] La celda no existe."
            );

            return null;
        }

        if (!cell.PuedePlantar())
        {
            Debug.LogWarning(
                "[PvZ] La celda ya está ocupada."
            );

            return null;
        }

        Plant planta =
            Instantiate(datos.prefab);

        planta.Inicializar(
            fila,
            columna,
            datos
        );

        if (PlantManager.Instancia != null)
        {
            PlantManager.Instancia.RegistrarPlanta(
                planta
            );
        }

        return planta;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}