using UnityEngine;
using UnityEngine.UI;

public class SeedPacket : MonoBehaviour
{
    [Header("Planta")]
    public PlantType tipo;

    [Header("Datos")]
    public PlantData datos;

    [Header("UI")]
    public Button boton;

    private void Awake()
    {
        if (boton == null)
            boton = GetComponent<Button>();

        if (boton != null)
            boton.onClick.AddListener(Seleccionar);
    }

    public void Seleccionar()
    {
        if (CursorManager.Instancia == null)
            return;

        if (datos == null)
        {
            Debug.LogWarning(
                "[PvZ] SeedPacket no tiene PlantData asignado."
            );

            return;
        }

        if (datos.tipo != tipo)
        {
            Debug.LogWarning(
                "[PvZ] El PlantData no coincide con el PlantType."
            );

            return;
        }

        if (SeedBank.Instancia != null &&
            !SeedBank.Instancia.PuedeComprar(datos))
        {
            return;
        }

        CursorManager.Instancia.Seleccionar(tipo);
    }
}