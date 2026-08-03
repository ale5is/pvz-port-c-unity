using System.Collections.Generic;
using UnityEngine;

public class PlantFactory : MonoBehaviour
{
    public static PlantFactory Instancia;

    public List<PlantData> plantas;

    private Dictionary<PlantType, PlantData> diccionario;

    private void Awake()
    {
        Instancia = this;

        diccionario = new Dictionary<PlantType, PlantData>();

        foreach (PlantData planta in plantas)
            diccionario.Add(planta.tipo, planta);
    }

    public Plant CrearPlanta(PlantType tipo, Cell celda)
    {
        if (!diccionario.TryGetValue(tipo, out PlantData datos))
            return null;

        Plant planta = Instantiate(datos.prefab, celda.posicion, Quaternion.identity);

        planta.datos = datos;
        planta.fila = celda.fila;
        planta.columna = celda.columna;

        celda.planta = planta;

        return planta;
    }
}