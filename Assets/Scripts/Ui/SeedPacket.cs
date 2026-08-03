using UnityEngine;
using UnityEngine.UI;

public class SeedPacket : MonoBehaviour
{
    public PlantType tipo;

    public void Seleccionar()
    {
        CursorManager.Instancia.Seleccionar(tipo);
    }
}