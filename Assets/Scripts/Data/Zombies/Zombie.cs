using UnityEngine;

public class Zombie : GameObject
{
    public ZombieData datos;

    protected int vida;
    protected int vidaArmadura;

    protected float temporizadorAtaque;

    public bool Muerto => !activo || vida <= 0;

    protected override void Start()
    {
        base.Start();

        if (datos != null)
        {
            vida = datos.vida;
            vidaArmadura = datos.vidaArmadura;
        }
    }

    public virtual void Inicializar(int row, ZombieData zombieData)
    {
        datos = zombieData;

        fila = row;
        columna = Board.COLUMNAS;

        if (datos != null)
        {
            vida = datos.vida;
            vidaArmadura = datos.vidaArmadura;
        }

        if (Board.Instancia != null)
        {
            transform.position =
                Board.Instancia.ObtenerPosicionZombie(row);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!activo || datos == null)
            return;

        ActualizarCombate();

        if (!activo)
            return;

        if (!EstaAtacando())
            Avanzar();
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
        temporizadorAtaque -= Time.deltaTime;

        Plant objetivo = BuscarPlanta();

        if (objetivo == null)
            return;

        float distancia =
            Mathf.Abs(
                transform.position.x -
                objetivo.transform.position.x
            );

        if (distancia <= datos.rangoAtaque &&
            temporizadorAtaque <= 0f)
        {
            objetivo.RecibirDaño(datos.daño);

            temporizadorAtaque =
                datos.intervaloAtaque;
        }
    }

    protected virtual bool EstaAtacando()
    {
        Plant planta = BuscarPlanta();

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

        int columna = Mathf.RoundToInt(
            (transform.position.x -
             Board.Instancia.origen.x)
            /
            Board.Instancia.anchoCelda
        );

        columna = Mathf.Clamp(
            columna,
            0,
            Board.COLUMNAS - 1
        );

        Cell cell =
            Board.Instancia.ObtenerCelda(
                fila,
                columna
            );

        if (cell == null)
            return null;

        return cell.planta;
    }

    public virtual void RecibirDaño(int daño)
    {
        if (daño <= 0 || Muerto)
            return;

        if (vidaArmadura > 0)
        {
            int dañoArmadura =
                Mathf.Min(
                    vidaArmadura,
                    daño
                );

            vidaArmadura -= dañoArmadura;
            daño -= dañoArmadura;
        }

        vida -= daño;

        if (vida <= 0)
            Morir();
    }

    protected virtual void Morir()
    {
        activo = false;

        if (ZombieManager.Instancia != null)
            ZombieManager.Instancia.NotificarMuerte(this);

        Destroy(gameObject);
    }
}
