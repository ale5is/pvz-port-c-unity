using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instancia;

    public PlantType plantaSeleccionada = PlantType.None;

    private void Awake()
    {
        Instancia = this;
    }

    public bool TienePlanta()
    {
        return plantaSeleccionada != PlantType.None;
    }

    public void Seleccionar(PlantType tipo)
    {
        plantaSeleccionada = tipo;
    }

    public void Cancelar()
    {
        plantaSeleccionada = PlantType.None;
    }
}