using UnityEngine;

public class Plant : GameObject
{
    [Header("Datos")]
    public PlantData datos;

    [Header("Vida")]
    public int vida;
    public int vidaMaxima;

    [Header("Ataque")]
    protected float temporizadorAtaque;

    [Header("Producción")]
    protected float temporizadorProduccion;

    protected override void Start()
    {
        base.Start();

        if (datos != null)
            InicializarVida();
    }

    protected override void Update()
    {
        if (!activo || muerto)
            return;

        base.Update();

        if (datos == null)
            return;

        ActualizarAtaque();
        ActualizarProduccion();
    }

    public virtual void Inicializar(
        int row,
        int col,
        PlantData plantData)
    {
        fila = row;
        columna = col;
        datos = plantData;

        activo = true;
        muerto = false;

        InicializarVida();

        temporizadorAtaque = 0f;
        temporizadorProduccion =
            datos != null
                ? datos.intervaloProduccion
                : 0f;

        if (Board.Instancia != null)
        {
            Cell cell =
                Board.Instancia.ObtenerCelda(
                    row,
                    col
                );

            if (cell != null)
            {
                transform.position =
                    cell.posicion;

                cell.ColocarPlanta(this);
            }
        }

        if (PlantManager.Instancia != null)
        {
            PlantManager.Instancia.RegistrarPlanta(
                this
            );
        }
    }

    protected virtual void InicializarVida()
    {
        if (datos == null)
            return;

        vidaMaxima =
            Mathf.Max(1, datos.vida);

        vida = vidaMaxima;
    }

    protected virtual void ActualizarAtaque()
    {
        if (!datos.puedeAtacar)
            return;

        if (datos.daño <= 0)
            return;

        if (datos.tipo == PlantType.WallNut ||
            datos.tipo == PlantType.TallNut ||
            datos.tipo == PlantType.Pumpkin)
        {
            return;
        }

        temporizadorAtaque -=
            Time.deltaTime;

        if (temporizadorAtaque > 0f)
            return;

        Zombie objetivo =
            BuscarZombie();

        if (objetivo == null)
            return;

        Atacar(objetivo);

        temporizadorAtaque =
            Mathf.Max(
                0.05f,
                datos.intervaloAtaque
            );
    }

    protected virtual Zombie BuscarZombie()
    {
        if (ZombieManager.Instancia == null)
            return null;

        Zombie objetivo = null;

        float distanciaMinima =
            float.MaxValue;

        foreach (
            Zombie zombie
            in ZombieManager.Instancia.ZombiesActivos)
        {
            if (zombie == null ||
                zombie.Muerto ||
                !zombie.activo)
            {
                continue;
            }

            if (zombie.fila != fila)
                continue;

            float distancia =
                zombie.transform.position.x -
                transform.position.x;

            if (distancia < -0.05f)
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

    protected virtual void Atacar(
        Zombie objetivo)
    {
        if (objetivo == null)
            return;

        if (datos.prefabProyectil == null)
        {
            objetivo.RecibirDaño(
                datos.daño
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

        proyectil.dañoEnArea =
            datos.dañoEnArea;

        proyectil.radioDaño =
            datos.radioDaño;

        proyectil.ralentiza =
            datos.ralentiza;

        proyectil.multiplicadorRalentizacion =
            datos.multiplicadorRalentizacion;
    }

    protected virtual void ActualizarProduccion()
    {
        if (!datos.puedeProducirSol)
            return;

        if (datos.produccionSol <= 0)
            return;

        temporizadorProduccion -=
            Time.deltaTime;

        if (temporizadorProduccion > 0f)
            return;

        ProducirSol();

        temporizadorProduccion =
            Mathf.Max(
                0.1f,
                datos.intervaloProduccion
            );
    }

    protected virtual void ProducirSol()
    {
        Debug.Log(
            "[PvZ] " +
            datos.nombre +
            " produjo " +
            datos.produccionSol +
            " soles."
        );

        if (SeedBank.Instancia != null)
        {
            SeedBank.Instancia.AgregarSol(
                datos.produccionSol
            );
        }
    }

    public virtual void RecibirDaño(
        int daño)
    {
        if (daño <= 0 ||
            !activo ||
            muerto)
        {
            return;
        }

        vida -= daño;

        if (vida <= 0)
            Morir();
    }

    protected virtual void Morir()
    {
        if (muerto)
            return;

        muerto = true;
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

            if (cell != null &&
                cell.planta == this)
            {
                cell.QuitarPlanta();
            }
        }

        Destroy(gameObject);
    }

    public override void Kill()
    {
        Morir();
    }

    public virtual void Curar(
        int cantidad)
    {
        if (cantidad <= 0 ||
            !activo ||
            muerto)
        {
            return;
        }

        vida =
            Mathf.Min(
                vida + cantidad,
                vidaMaxima
            );
    }

    public float PorcentajeVida()
    {
        if (vidaMaxima <= 0)
            return 0f;

        return Mathf.Clamp01(
            (float)vida /
            vidaMaxima
        );
    }

    public bool EstaHerida()
    {
        return vida < vidaMaxima;
    }

    public bool EstaViva()
    {
        return activo &&
               !muerto &&
               vida > 0;
    }
}