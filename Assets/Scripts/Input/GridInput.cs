using UnityEngine;

public class GridInput : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            Plane plane = new Plane(Vector3.forward, Vector3.zero);

            if (plane.Raycast(ray, out float distancia))
            {
                Vector3 punto = ray.GetPoint(distancia);

                Cell celda = Board.Instancia.ObtenerCeldaDesdeMundo(punto);

                if (celda != null)
                {
                    if (!CursorManager.Instancia.TienePlanta())
                        return;

                    if (!celda.Ocupada)
                    {
                        PlantFactory.Instancia.CrearPlanta(
                            CursorManager.Instancia.plantaSeleccionada,
                            celda);

                        CursorManager.Instancia.Cancelar();
                    }
                }
            }
        }
    }
}