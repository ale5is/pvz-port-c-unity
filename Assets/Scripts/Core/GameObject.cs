using UnityEngine;

public class GameObject : MonoBehaviour
{
    [Header("Posición en el tablero")]
    public int fila;
    public int columna;

    [Header("Estado")]
    public bool visible = true;

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
}