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

    public Cell[,] celdas { get; private set; }

    private void Awake()
    {
        Instancia = this;

        CrearTablero();
    }

    private void CrearTablero()
    {
        celdas = new Cell[FILAS, COLUMNAS];

        for (int fila = 0; fila < FILAS; fila++)
        {
            for (int columna = 0; columna < COLUMNAS; columna++)
            {
                Vector3 posicion = origen + new Vector3(
                    columna * anchoCelda,
                    -fila * altoCelda,
                    0f
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

    public Cell ObtenerCelda(int fila, int columna)
    {
        if (celdas == null)
            return null;

        if (fila < 0 || fila >= FILAS)
            return null;

        if (columna < 0 || columna >= COLUMNAS)
            return null;

        return celdas[fila, columna];
    }

    public Cell ObtenerCeldaDesdeMundo(
        Vector3 posicionMundo)
    {
        int columna = Mathf.RoundToInt(
            (posicionMundo.x - origen.x) /
            anchoCelda
        );

        int fila = Mathf.RoundToInt(
            (origen.y - posicionMundo.y) /
            altoCelda
        );

        return ObtenerCelda(fila, columna);
    }

    public Vector3 ObtenerPosicionZombie(int fila)
    {
        Cell ultimaCelda =
            ObtenerCelda(
                fila,
                COLUMNAS - 1
            );

        if (ultimaCelda == null)
            return origen;

        return ultimaCelda.posicion +
               Vector3.right * anchoCelda;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        for (int fila = 0; fila < FILAS; fila++)
        {
            for (int columna = 0; columna < COLUMNAS; columna++)
            {
                Vector3 posicion =
                    origen +
                    new Vector3(
                        columna * anchoCelda,
                        -fila * altoCelda,
                        0f
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