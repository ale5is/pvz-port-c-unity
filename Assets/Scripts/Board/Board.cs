using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    public static Board Instancia { get; private set; }

    public const int FILAS = 5;
    public const int COLUMNAS = 9;

    [Header("Tamaño de las celdas")]
    public float anchoCelda = 1.6f;
    public float altoCelda = 1.8f;

    [Header("Origen del tablero")]
    public Vector3 origen = Vector3.zero;

    [Header("Configuración")]
    public bool crearAlIniciar = true;

    public Cell[,] celdas { get; private set; }

    private void Awake()
    {
        if (Instancia != null &&
            Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;

        if (crearAlIniciar)
            CrearTablero();
    }

    public void CrearTablero()
    {
        celdas = new Cell[FILAS, COLUMNAS];

        for (int fila = 0; fila < FILAS; fila++)
        {
            for (int columna = 0;
                 columna < COLUMNAS;
                 columna++)
            {
                Vector3 posicion =
                    ObtenerPosicionCelda(
                        fila,
                        columna
                    );

                celdas[fila, columna] =
                    new Cell(
                        fila,
                        columna,
                        posicion
                    );
            }
        }
    }

    public Vector3 ObtenerPosicionCelda(
        int fila,
        int columna)
    {
        return origen +
               new Vector3(
                   columna * anchoCelda,
                   -fila * altoCelda,
                   0f
               );
    }

    public bool EsFilaValida(int fila)
    {
        return fila >= 0 &&
               fila < FILAS;
    }

    public bool EsColumnaValida(int columna)
    {
        return columna >= 0 &&
               columna < COLUMNAS;
    }

    public bool EsCeldaValida(
        int fila,
        int columna)
    {
        return EsFilaValida(fila) &&
               EsColumnaValida(columna);
    }

    public Cell ObtenerCelda(
        int fila,
        int columna)
    {
        if (celdas == null)
            CrearTablero();

        if (!EsCeldaValida(fila, columna))
            return null;

        return celdas[fila, columna];
    }

    public Cell ObtenerCeldaDesdeMundo(
        Vector3 posicionMundo)
    {
        if (celdas == null)
            CrearTablero();

        float diferenciaX =
            posicionMundo.x - origen.x;

        float diferenciaY =
            origen.y - posicionMundo.y;

        int columna =
            Mathf.RoundToInt(
                diferenciaX / anchoCelda
            );

        int fila =
            Mathf.RoundToInt(
                diferenciaY / altoCelda
            );

        return ObtenerCelda(
            fila,
            columna
        );
    }

    public bool PuedePlantar(
        int fila,
        int columna)
    {
        Cell celda =
            ObtenerCelda(
                fila,
                columna
            );

        return celda != null &&
               celda.PuedePlantar();
    }

    public bool ColocarPlanta(
        Plant planta,
        int fila,
        int columna)
    {
        if (planta == null)
            return false;

        Cell celda =
            ObtenerCelda(
                fila,
                columna
            );

        if (celda == null)
            return false;

        if (!celda.ColocarPlanta(planta))
            return false;

        planta.transform.position =
            celda.posicion;

        return true;
    }

    public void QuitarPlanta(
        int fila,
        int columna)
    {
        Cell celda =
            ObtenerCelda(
                fila,
                columna
            );

        if (celda != null)
            celda.QuitarPlanta();
    }

    public Plant ObtenerPlanta(
        int fila,
        int columna)
    {
        Cell celda =
            ObtenerCelda(
                fila,
                columna
            );

        return celda?.planta;
    }

    public List<Plant> ObtenerPlantasEnFila(
        int fila)
    {
        List<Plant> resultado =
            new List<Plant>();

        if (!EsFilaValida(fila))
            return resultado;

        for (int columna = 0;
             columna < COLUMNAS;
             columna++)
        {
            Plant planta =
                celdas[fila, columna].planta;

            if (planta != null &&
                planta.activo)
            {
                resultado.Add(planta);
            }
        }

        return resultado;
    }

    public Plant ObtenerPrimeraPlantaEnFila(
        int fila)
    {
        if (!EsFilaValida(fila))
            return null;

        for (int columna = 0;
             columna < COLUMNAS;
             columna++)
        {
            Plant planta =
                celdas[fila, columna].planta;

            if (planta != null &&
                planta.activo)
            {
                return planta;
            }
        }

        return null;
    }

    public Vector3 ObtenerPosicionZombie(
        int fila)
    {
        if (!EsFilaValida(fila))
            return origen;

        Cell ultimaCelda =
            ObtenerCelda(
                fila,
                COLUMNAS - 1
            );

        if (ultimaCelda == null)
            return origen;

        return ultimaCelda.posicion +
               Vector3.right *
               anchoCelda;
    }

    public Vector3 ObtenerPosicionFueraDelTablero(
        int fila)
    {
        return ObtenerPosicionZombie(fila) +
               Vector3.right * 1.5f;
    }

    public void LimpiarTablero()
    {
        if (celdas == null)
            return;

        for (int fila = 0;
             fila < FILAS;
             fila++)
        {
            for (int columna = 0;
                 columna < COLUMNAS;
                 columna++)
            {
                celdas[fila, columna].Limpiar();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        for (int fila = 0;
             fila < FILAS;
             fila++)
        {
            for (int columna = 0;
                 columna < COLUMNAS;
                 columna++)
            {
                Vector3 posicion =
                    ObtenerPosicionCelda(
                        fila,
                        columna
                    );

                Gizmos.DrawWireCube(
                    posicion,
                    new Vector3(
                        anchoCelda,
                        altoCelda,
                        0.05f
                    )
                );
            }
        }
    }
}