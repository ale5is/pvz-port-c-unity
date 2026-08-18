using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instancia { get; private set; }

    [Header("Estado")]
    public PlantType plantaSeleccionada = PlantType.None;

    public bool HayPlantaSeleccionada =>
        plantaSeleccionada != PlantType.None;

    private void Awake()
    {
        if (Instancia != null &&
            Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    public void SeleccionarPlanta(
        PlantType tipo)
    {
        plantaSeleccionada = tipo;
    }

    public void CancelarSeleccion()
    {
        plantaSeleccionada = PlantType.None;
    }

    public PlantType ObtenerPlantaSeleccionada()
    {
        return plantaSeleccionada;
    }

    public bool TieneSeleccion()
    {
        return plantaSeleccionada != PlantType.None;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}