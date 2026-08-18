using UnityEngine;

public class Cell
{
    public int fila;
    public int columna;
    public Vector3 posicion;

    public Plant planta;

    public bool Ocupada => planta != null;

    public Cell(int fila, int columna, Vector3 posicion)
    {
        this.fila = fila;
        this.columna = columna;
        this.posicion = posicion;
    }

    public bool PuedePlantar()
    {
        return planta == null;
    }

    public bool ColocarPlanta(Plant nuevaPlanta)
    {
        if (nuevaPlanta == null || Ocupada)
            return false;

        planta = nuevaPlanta;

        nuevaPlanta.fila = fila;
        nuevaPlanta.columna = columna;

        return true;
    }

    public void QuitarPlanta()
    {
        planta = null;
    }
}