using UnityEngine;

public class Cell
{
    public int fila;
    public int columna;
    public Vector3 posicion;

    public Plant planta;

    public bool Ocupada =>
        planta != null &&
        planta.activo;

    public Cell(
        int fila,
        int columna,
        Vector3 posicion)
    {
        this.fila = fila;
        this.columna = columna;
        this.posicion = posicion;
    }

    public bool TienePlanta()
    {
        return planta != null &&
               planta.activo;
    }

    public bool PuedePlantar()
    {
        return !TienePlanta();
    }

    public bool ColocarPlanta(Plant nuevaPlanta)
    {
        if (nuevaPlanta == null)
            return false;

        if (!PuedePlantar())
            return false;

        planta = nuevaPlanta;

        nuevaPlanta.fila = fila;
        nuevaPlanta.columna = columna;

        return true;
    }

    public Plant ObtenerPlanta()
    {
        return planta;
    }

    public void QuitarPlanta()
    {
        planta = null;
    }

    public void Limpiar()
    {
        planta = null;
    }
}