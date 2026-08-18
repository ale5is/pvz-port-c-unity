using UnityEngine;

public class GameObject : MonoBehaviour
{
    [Header("Identidad")]
    public int id;

    [Header("Posición en el tablero")]
    public int fila;
    public int columna;

    [Header("Estado")]
    public bool visible = true;
    public bool activo = true;
    public bool muerto;

    protected virtual void Awake()
    {
        activo = true;
        muerto = false;
        visible = true;
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        if (!activo)
            return;

        Tick();
    }

    public virtual void Tick()
    {
    }

    public virtual void Inicializar(int fila, int columna)
    {
        this.fila = fila;
        this.columna = columna;

        activo = true;
        muerto = false;
        visible = true;
    }

    public virtual Vector3 ObtenerPosicion()
    {
        return transform.position;
    }

    public virtual void SetFila(int nuevaFila)
    {
        if (Board.Instancia == null)
            return;

        nuevaFila = Mathf.Clamp(
            nuevaFila,
            0,
            Board.FILAS - 1
        );

        fila = nuevaFila;
    }

    public virtual void SetColumna(int nuevaColumna)
    {
        if (Board.Instancia == null)
            return;

        nuevaColumna = Mathf.Clamp(
            nuevaColumna,
            0,
            Board.COLUMNAS - 1
        );

        columna = nuevaColumna;
    }

    public virtual void Kill()
    {
        if (muerto)
            return;

        muerto = true;
        activo = false;
        visible = false;

        Destroy(gameObject);
    }
}