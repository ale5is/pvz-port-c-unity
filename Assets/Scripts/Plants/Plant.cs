using UnityEngine;

public class Plant : GameObject
{
    [Header("Datos de la planta")]
    public PlantData datos;

    [Header("Estado")]
    public int vida;
    public int vidaMaxima;

    [Header("Ataque")]
    protected float temporizadorAtaque;

    protected override void Start()
    {
        base.Start();

        InicializarVida();
    }

    protected override void Update()
    {
        base.Update();

        if (!activo || datos == null)
            return;

        ActualizarAtaque();
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

        temporizadorAtaque = 0f;

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

        if (PlantManager.Instancia != null)
        {
            PlantManager.Instancia.RegistrarPlanta(this);
        }
    }

    protected virtual void InicializarVida()
    {
        if (datos == null)
            return;

        vidaMaxima = datos.vida;
        vida = vidaMaxima;
    }

    protected virtual void ActualizarAtaque()
    {
        if (datos.tipo != PlantType.Peashooter)
            return;

        temporizadorAtaque -= Time.deltaTime;

        if (temporizadorAtaque > 0f)
            return;

        Zombie objetivo = BuscarZombie();

        if (objetivo == null)
            return;

        Atacar(objetivo);

        temporizadorAtaque =
            datos.intervaloAtaque;
    }

    protected virtual Zombie BuscarZombie()
    {
        if (ZombieManager.Instancia == null)
            return null;

        Zombie objetivo = null;
        float distanciaMinima = float.MaxValue;

        foreach (
            Zombie zombie
            in ZombieManager.Instancia.ZombiesActivos)
        {
            if (zombie == null ||
                zombie.Muerto ||
                zombie.fila != fila)
            {
                continue;
            }

            float distancia =
                zombie.transform.position.x -
                transform.position.x;

            if (distancia < 0f)
                continue;

            if (distancia > datos.rangoAtaque)
                continue;

            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                objetivo = zombie;
            }
        }

        return objetivo;
    }

    protected virtual void Atacar(Zombie objetivo)
    {
        if (objetivo == null)
            return;

        if (datos.prefabProyectil == null)
        {
            Debug.LogWarning(
                "[PvZ] La planta '" +
                datos.nombre +
                "' no tiene prefab de proyectil."
            );

            return;
        }

        Projectiles proyectil =
            Instantiate(
                datos.prefabProyectil,
                transform.position,
                Quaternion.identity
            );

        proyectil.Inicializar(
            fila,
            datos.daño,
            datos.velocidadProyectil
        );
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

        if (PlantManager.Instancia != null)
        {
            PlantManager.Instancia.EliminarPlanta(
                this
            );
        }

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