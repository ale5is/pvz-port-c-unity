using UnityEngine;

public class GameObject : MonoBehaviour
{
    [Header("Posición en el tablero")]
    public int fila;
    public int columna;

    [Header("Estado")]
    public bool visible = true;
    public bool activo = true;

    protected virtual void Awake()
    {
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
    }

    public virtual void Tick()
    {
    }

    public virtual void Inicializar(int fila, int columna)
    {
        this.fila = fila;
        this.columna = columna;
        activo = true;
    }

    public virtual void Kill()
    {
        activo = false;
        Destroy(gameObject);
    }
}