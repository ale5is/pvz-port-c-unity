using System.Collections.Generic;
using UnityEngine;

public class PlantManager : MonoBehaviour
{
    public static PlantManager Instancia { get; private set; }

    private readonly List<Plant> plantas = new();

    public IReadOnlyList<Plant> Plantas => plantas;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    public void RegistrarPlanta(Plant planta)
    {
        if (planta == null)
            return;

        if (!plantas.Contains(planta))
            plantas.Add(planta);
    }

    public void EliminarPlanta(Plant planta)
    {
        if (planta == null)
            return;

        plantas.Remove(planta);
    }

    public Plant ObtenerPlanta(int fila, int columna)
    {
        for (int i = plantas.Count - 1; i >= 0; i--)
        {
            Plant planta = plantas[i];

            if (planta == null)
            {
                plantas.RemoveAt(i);
                continue;
            }

            if (!planta.activo)
                continue;

            if (planta.fila == fila &&
                planta.columna == columna)
            {
                return planta;
            }
        }

        return null;
    }

    public List<Plant> ObtenerPlantasEnFila(int fila)
    {
        List<Plant> resultado = new();

        for (int i = plantas.Count - 1; i >= 0; i--)
        {
            Plant planta = plantas[i];

            if (planta == null)
            {
                plantas.RemoveAt(i);
                continue;
            }

            if (!planta.activo)
                continue;

            if (planta.fila == fila)
                resultado.Add(planta);
        }

        resultado.Sort(
            (a, b) =>
                a.transform.position.x.CompareTo(
                    b.transform.position.x
                )
        );

        return resultado;
    }

    public Plant ObtenerPrimeraPlantaEnFila(int fila)
    {
        Plant objetivo = null;

        foreach (Plant planta in ObtenerPlantasEnFila(fila))
        {
            if (objetivo == null ||
                planta.transform.position.x >
                objetivo.transform.position.x)
            {
                objetivo = planta;
            }
        }

        return objetivo;
    }

    public void LimpiarPlantas()
    {
        for (int i = plantas.Count - 1; i >= 0; i--)
        {
            if (plantas[i] != null)
                Destroy(plantas[i].gameObject);
        }

        plantas.Clear();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}