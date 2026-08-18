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

        if (PlantFactory.Instancia == null)
        {
            Debug.LogError("[PvZ] No existe PlantFactory.");
            return null;
        }

        if (Board.Instancia == null)
        {
            Debug.LogError("[PvZ] No existe Board.");
            return null;
        }

        Cell celda =
            Board.Instancia.ObtenerCelda(
                fila,
                columna
            );

        if (celda == null)
        {
            Debug.LogWarning(
                $"[PvZ] Celda inválida: {fila},{columna}"
            );

            return null;
        }

        if (!celda.PuedePlantar())
        {
            Debug.LogWarning(
                $"[PvZ] La celda {fila},{columna} ya está ocupada."
            );

            return null;
        }

        Plant planta =
            PlantFactory.Instancia.CrearPlanta(
                datos,
                fila,
                columna
            );

        if (planta != null)
        {
            RegistrarPlanta(planta);
        }

        return planta;
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

    public Plant ObtenerPlanta(
        int fila,
        int columna)
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
            {
                plantas.RemoveAt(i);
                continue;
            }

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