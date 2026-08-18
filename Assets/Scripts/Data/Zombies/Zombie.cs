using UnityEngine;

public class Zombie : GameObject
{
    [Header("Datos")]
    public ZombieData datos;

    [Header("Vida")]
    protected int vida;
    protected int vidaArmadura;

    [Header("Ataque")]
    protected float temporizadorAtaque;

    public bool Muerto =>
        !activo || vida <= 0;

    protected override void Start()
    {
        base.Start();

        if (datos != null)
        {
            vida = datos.vida;
            vidaArmadura =
                datos.vidaArmadura;
        }
    }

    public virtual void Inicializar(
        int row,
        ZombieData zombieData)
    {
        datos = zombieData;

        fila = row;
        columna = Board.COLUMNAS;

        if (datos != null)
        {
            vida = datos.vida;
            vidaArmadura =
                datos.vidaArmadura;
        }

        temporizadorAtaque = 0f;

        if (Board.Instancia != null)
        {
            transform.position =
                ObtenerPosicionInicial(row);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!activo ||
            datos == null)
        {
            return;
        }

        ActualizarCombate();

        if (!activo)
            return;

        if (!EstaAtacando())
            Avanzar();

        ComprobarLimite();
    }

    protected virtual Vector3 ObtenerPosicionInicial(
        int row)
    {
        Cell ultimaCelda =
            Board.Instancia.ObtenerCelda(
                row,
                Board.COLUMNAS - 1
            );

        if (ultimaCelda == null)
            return Board.Instancia.origen;

        return ultimaCelda.posicion +
            Vector3.right *
            Board.Instancia.anchoCelda;
    }

    protected virtual void Avanzar()
    {
        transform.position +=
            Vector3.left *
            datos.velocidad *
            Time.deltaTime;
    }

    protected virtual void ActualizarCombate()
    {
        temporizadorAtaque -=
            Time.deltaTime;

        Plant objetivo =
            BuscarPlanta();

        if (objetivo == null)
            return;

        float distancia =
            Mathf.Abs(
                transform.position.x -
                objetivo.transform.position.x
            );

        if (distancia <=
                datos.rangoAtaque &&
            temporizadorAtaque <= 0f)
        {
            objetivo.RecibirDaño(
                datos.daño
            );

            temporizadorAtaque =
                datos.intervaloAtaque;
        }
    }

    protected virtual bool EstaAtacando()
    {
        Plant planta =
            BuscarPlanta();

        if (planta == null)
            return false;

        return Mathf.Abs(
            transform.position.x -
            planta.transform.position.x
        ) <= datos.rangoAtaque;
    }

    protected virtual Plant BuscarPlanta()
    {
        if (Board.Instancia == null)
            return null;

        PlantManager manager =
            PlantManager.Instancia;

        if (manager == null)
            return null;

        return manager.ObtenerPrimeraPlantaEnFila(
            fila
        );
    }

    public virtual void RecibirDaño(
        int daño)
    {
        if (daño <= 0 ||
            Muerto)
        {
            return;
        }

        if (vidaArmadura > 0)
        {
            int dañoArmadura =
                Mathf.Min(
                    vidaArmadura,
                    daño
                );

            vidaArmadura -=
                dañoArmadura;

            daño -=
                dañoArmadura;
        }

        if (daño > 0)
            vida -= daño;

        if (vida <= 0)
            Morir();
    }

    protected virtual void Morir()
    {
        if (!activo)
            return;

        activo = false;

        if (ZombieManager.Instancia != null)
        {
            ZombieManager.Instancia
                .NotificarMuerte(this);
        }

        Destroy(gameObject);
    }

    protected virtual void ComprobarLimite()
    {
        if (Board.Instancia == null)
            return;

        float limite =
            Board.Instancia.origen.x -
            Board.Instancia.anchoCelda;

        if (transform.position.x < limite)
        {
            activo = false;

            if (ZombieManager.Instancia != null)
            {
                ZombieManager.Instancia
                    .NotificarMuerte(this);
            }

            Destroy(gameObject);
        }
    }
}