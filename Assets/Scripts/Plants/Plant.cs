using UnityEngine;

public class Plant : GameObject
{
    [Header("Datos de la planta")]
    public PlantData datos;

    [Header("Estado")]
    public int vida;
    public int vidaMaxima;

    protected override void Start()
    {
        base.Start();

        InicializarVida();
    }

    public virtual void Inicializar(
        int row,
        int col,
        PlantData plantData)
    {
        fila = row;
        columna = col;

        datos = plantData;

        InicializarVida();

        if (Board.Instancia != null)
        {
            Cell cell =
                Board.Instancia.ObtenerCelda(
                    row,
                    col
                );

            if (cell != null)
            {
                transform.position = cell.posicion;
                cell.ColocarPlanta(this);
            }
        }
    }

    protected virtual void InicializarVida()
    {
        if (datos == null)
            return;

        vidaMaxima = datos.vida;
        vida = vidaMaxima;
    }

    public virtual void RecibirDaño(int daño)
    {
        if (daño <= 0 || !activo)
            return;

        vida -= daño;

        if (vida <= 0)
            Morir();
    }

    protected virtual void Morir()
    {
        activo = false;

        if (Board.Instancia != null)
        {
            Cell cell =
                Board.Instancia.ObtenerCelda(
                    fila,
                    columna
                );

            if (cell != null)
                cell.QuitarPlanta();
        }

        Destroy(gameObject);
    }

    public virtual void Curar(int cantidad)
    {
        if (cantidad <= 0 || !activo)
            return;

        vida = Mathf.Min(
            vida + cantidad,
            vidaMaxima
        );
    }
}