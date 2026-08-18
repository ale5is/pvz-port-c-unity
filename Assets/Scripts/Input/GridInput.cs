using UnityEngine;

public class GridInput : MonoBehaviour
{
    [Header("Plantas")]
    public PlantData[] plantas;

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (Board.Instancia == null ||
            CursorManager.Instancia == null ||
            PlantFactory.Instancia == null)
            return;

        if (!CursorManager.Instancia.TieneSeleccion())
            return;

        Camera camara = Camera.main;

        if (camara == null)
            return;

        Ray ray =
            camara.ScreenPointToRay(
                Input.mousePosition
            );

        Plane plano =
            new Plane(
                Vector3.forward,
                Vector3.zero
            );

        if (!plano.Raycast(
                ray,
                out float distancia))
        {
            return;
        }

        Vector3 punto =
            ray.GetPoint(distancia);

        Cell celda =
            Board.Instancia.ObtenerCeldaDesdeMundo(
                punto
            );

        if (celda == null ||
            celda.Ocupada)
        {
            return;
        }

        PlantType tipo =
            CursorManager.Instancia
                .ObtenerPlantaSeleccionada();

        PlantData datos =
            ObtenerDatos(tipo);

        if (datos == null)
            return;

        if (SeedBank.Instancia != null &&
            !SeedBank.Instancia.PuedePlantar(datos))
        {
            return;
        }

        Plant planta =
            PlantFactory.Instancia.CrearPlanta(
                datos,
                celda.fila,
                celda.columna
            );

        if (planta == null)
            return;

        if (SeedBank.Instancia != null)
        {
            if (!SeedBank.Instancia.ComprarPlanta(datos))
            {
                Destroy(planta.gameObject);
                return;
            }
        }

        CursorManager.Instancia.CancelarSeleccion();
    }

    private PlantData ObtenerDatos(
        PlantType tipo)
    {
        if (plantas == null)
            return null;

        foreach (PlantData datos in plantas)
        {
            if (datos == null)
                continue;

            if (datos.tipo == tipo)
                return datos;
        }

        Debug.LogWarning(
            "[PvZ] No existe PlantData para: " +
            tipo
        );

        return null;
    }
}