using UnityEngine;

public class Plant : GameObject
{
    [Header("Datos")]
    public PlantData datos;

    [Header("Vida")]
    public int vida;
    public int vidaMaxima;

    [Header("Estado")]
    public bool congelada;
    public bool ralentizada;
    public bool aturdida;

    [Header("Combate")]
    public Zombie objetivo;

    private float temporizadorAtaque;
    private float temporizadorEstado;
    private float temporizadorProduccion;

    private PvZReanimAnimator reanimAnimator;

    public bool Muerto => muerto;

    protected override void Start()
    {
        base.Start();

        ConfigurarAnimacion();

        if (datos != null &&
            vidaMaxima <= 0)
        {
            InicializarVida();
        }

        ReproducirIdle();
    }

    protected override void Update()
    {
        if (!activo || muerto)
            return;

        ActualizarEstados();

        if (muerto)
            return;

        ActualizarProduccion();

        if (datos != null &&
            datos.puedeAtacar)
        {
            BuscarObjetivo();

            if (objetivo != null)
                Atacar();
        }
    }

    private void ConfigurarAnimacion()
    {
        reanimAnimator =
            GetComponent<PvZReanimAnimator>();

        if (reanimAnimator == null)
        {
            reanimAnimator =
                gameObject.AddComponent<PvZReanimAnimator>();
        }

        if (datos != null &&
            !string.IsNullOrWhiteSpace(
                datos.reanimNombre))
        {
            reanimAnimator.ConfigurarReanim(
                datos.reanimNombre
            );
        }
    }

    public void Inicializar(
        int row,
        int column,
        PlantData plantData)
    {
        fila = row;
        columna = column;

        datos = plantData;

        activo = true;
        muerto = false;

        congelada = false;
        ralentizada = false;
        aturdida = false;

        temporizadorAtaque = 0f;
        temporizadorEstado = 0f;
        temporizadorProduccion = 0f;

        InicializarVida();

        ConfigurarAnimacion();

        ReproducirIdle();

        if (Board.Instancia != null)
        {
            transform.position =
                Board.Instancia.ObtenerPosicionCelda(
                    fila,
                    columna
                );
        }
    }

    private void InicializarVida()
    {
        if (datos == null)
            return;

        vidaMaxima =
            Mathf.Max(
                1,
                datos.vida
            );

        vida =
            vidaMaxima;
    }

    private void ActualizarProduccion()
    {
        if (datos == null ||
            !datos.puedeProducirSol)
        {
            return;
        }

        temporizadorProduccion -=
            Time.deltaTime;

        if (temporizadorProduccion > 0f)
            return;

        temporizadorProduccion =
            Mathf.Max(
                0.1f,
                datos.intervaloProduccion
            );

        ProducirSol();
    }

    private void ProducirSol()
    {
        /*
         * La generación real de Sol se conecta
         * con el sistema de SunManager/SeedBank.
         */
    }

    private void BuscarObjetivo()
    {
        objetivo = null;

        if (ZombieManager.Instancia == null)
            return;

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

            if (distancia < 0f)
                continue;

            if (distancia >
                ObtenerRangoAtaque())
            {
                continue;
            }

            if (distancia <
                distanciaMinima)
            {
                distanciaMinima =
                    distancia;

                objetivo =
                    zombie;
            }
        }
    }

    private float ObtenerRangoAtaque()
    {
        if (datos == null)
            return 10f;

        return Mathf.Max(
            0.1f,
            datos.rangoAtaque
        );
    }

    private void Atacar()
    {
        if (objetivo == null)
            return;

        if (objetivo.Muerto ||
            !objetivo.activo)
        {
            objetivo = null;
            return;
        }

        if (congelada ||
            aturdida)
        {
            return;
        }

        temporizadorAtaque -=
            Time.deltaTime;

        if (temporizadorAtaque > 0f)
            return;

        temporizadorAtaque =
            datos != null
                ? Mathf.Max(
                    0.05f,
                    datos.intervaloAtaque
                )
                : 1f;

        ReproducirAtaque();

        if (datos != null &&
            datos.prefabProyectil != null)
        {
            CrearProyectil();
        }
        else if (datos != null)
        {
            objetivo.RecibirDaño(
                datos.daño
            );
        }
    }

    private void CrearProyectil()
    {
        if (datos == null ||
            datos.prefabProyectil == null)
        {
            return;
        }

        Projectiles proyectil =
            Instantiate(
                datos.prefabProyectil,
                transform.position,
                Quaternion.identity
            );

        if (proyectil == null)
            return;

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

    public void RecibirDaño(
        int daño)
    {
        if (daño <= 0 ||
            muerto ||
            !activo)
        {
            return;
        }

        vida -= daño;

        if (vida <= 0)
        {
            vida = 0;
            Morir();
        }
    }

    public void Ralentizar(
        float duracion)
    {
        if (muerto)
            return;

        ralentizada = true;

        temporizadorEstado =
            Mathf.Max(
                temporizadorEstado,
                duracion
            );
    }

    public void Congelar(
        float duracion)
    {
        if (muerto)
            return;

        congelada = true;

        temporizadorEstado =
            Mathf.Max(
                temporizadorEstado,
                duracion
            );
    }

    public void Aturdir(
        float duracion)
    {
        if (muerto)
            return;

        aturdida = true;

        temporizadorEstado =
            Mathf.Max(
                temporizadorEstado,
                duracion
            );
    }

    private void ActualizarEstados()
    {
        if (temporizadorEstado <= 0f)
            return;

        temporizadorEstado -=
            Time.deltaTime;

        if (temporizadorEstado > 0f)
            return;

        congelada = false;
        ralentizada = false;
        aturdida = false;
    }

    private void ReproducirIdle()
    {
        if (reanimAnimator == null ||
            datos == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                datos.animacionIdle))
        {
            return;
        }

        reanimAnimator.Idle(
            datos.animacionIdle
        );
    }

    private void ReproducirAtaque()
    {
        if (reanimAnimator == null ||
            datos == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                datos.animacionAtaque))
        {
            return;
        }

        reanimAnimator.Atacar(
            datos.animacionAtaque
        );
    }

    private void Morir()
    {
        if (muerto)
            return;

        muerto = true;
        activo = false;

        objetivo = null;

        if (reanimAnimator != null &&
            datos != null &&
            !string.IsNullOrWhiteSpace(
                datos.animacionMuerte))
        {
            reanimAnimator.Morir(
                datos.animacionMuerte
            );
        }

        Destroy(
            gameObject,
            0.5f
        );
    }

    public override void Kill()
    {
        Morir();
    }

    public bool EstaViva()
    {
        return activo &&
               !muerto &&
               vida > 0;
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
}