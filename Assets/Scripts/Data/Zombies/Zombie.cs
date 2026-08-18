using UnityEngine;

public class Zombie : GameObject
{
    [Header("Datos")]
    public ZombieData datos;

    [Header("Vida")]
    public int vida;
    public int vidaMaxima;

    [Header("Estado")]
    public bool congelado;
    public bool ralentizado;
    public bool aturdido;

    [Header("Movimiento")]
    public float velocidadActual;

    [Header("Ataque")]
    public Plant plantaObjetivo;

    private float temporizadorAtaque;
    private float temporizadorEstado;

    public bool Muerto => muerto;

    protected override void Start()
    {
        base.Start();

        if (datos != null && vidaMaxima <= 0)
            InicializarVida();
    }

    protected override void Update()
    {
        if (!activo || muerto)
            return;

        ActualizarEstados();

        if (muerto)
            return;

        BuscarPlanta();

        if (plantaObjetivo != null)
        {
            AtacarPlanta();
        }
        else
        {
            Caminar();
        }
    }

    public void Inicializar(
        int row,
        ZombieData zombieData)
    {
        fila = row;
        columna = Board.COLUMNAS;

        datos = zombieData;

        activo = true;
        muerto = false;

        congelado = false;
        ralentizado = false;
        aturdido = false;

        InicializarVida();

        velocidadActual =
            datos != null
                ? datos.velocidad
                : 0.2f;

        temporizadorAtaque = 0f;

        if (Board.Instancia != null)
        {
            transform.position =
                Board.Instancia
                    .ObtenerPosicionFueraDelTablero(
                        fila
                    );
        }

        if (ZombieManager.Instancia != null)
        {
            ZombieManager.Instancia.RegistrarZombie(
                this
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
                datos.VidaTotal()
            );

        vida = vidaMaxima;
    }

    private void Caminar()
    {
        if (congelado ||
            aturdido)
            return;

        float velocidad =
            velocidadActual;

        if (ralentizado &&
            datos != null)
        {
            velocidad *=
                0.5f;
        }

        transform.position +=
            Vector3.left *
            velocidad *
            Time.deltaTime;

        ActualizarColumna();
    }

    private void ActualizarColumna()
    {
        if (Board.Instancia == null)
            return;

        float distancia =
            transform.position.x -
            Board.Instancia.origen.x;

        columna =
            Mathf.FloorToInt(
                distancia /
                Board.Instancia.anchoCelda
            );

        columna =
            Mathf.Clamp(
                columna,
                -1,
                Board.COLUMNAS
            );
    }

    private void BuscarPlanta()
    {
        plantaObjetivo = null;

        if (Board.Instancia == null)
            return;

        if (!Board.Instancia.EsFilaValida(fila))
            return;

        var plantas =
            Board.Instancia
                .ObtenerPlantasEnFila(fila);

        float distanciaMinima =
            float.MaxValue;

        foreach (Plant planta in plantas)
        {
            if (planta == null ||
                planta.muerto ||
                !planta.activo)
            {
                continue;
            }

            float distancia =
                transform.position.x -
                planta.transform.position.x;

            if (distancia < -0.2f)
                continue;

            if (distancia >
                ObtenerRangoAtaque())
            {
                continue;
            }

            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                plantaObjetivo = planta;
            }
        }
    }

    private float ObtenerRangoAtaque()
    {
        if (datos == null)
            return 0.7f;

        return Mathf.Max(
            0.1f,
            datos.rangoAtaque
        );
    }

    private void AtacarPlanta()
    {
        if (plantaObjetivo == null)
            return;

        if (plantaObjetivo.muerto ||
            !plantaObjetivo.activo)
        {
            plantaObjetivo = null;
            return;
        }

        temporizadorAtaque -=
            Time.deltaTime;

        if (temporizadorAtaque > 0f)
            return;

        int daño =
            datos != null
                ? Mathf.Max(0, datos.daño)
                : 20;

        plantaObjetivo.RecibirDaño(
            daño
        );

        temporizadorAtaque =
            datos != null
                ? Mathf.Max(
                    0.05f,
                    datos.intervaloAtaque
                )
                : 1f;
    }

    public void RecibirDaño(int daño)
    {
        if (daño <= 0 ||
            muerto ||
            !activo)
        {
            return;
        }

        int dañoRestante = daño;

        if (datos != null &&
            datos.vidaEscudo > 0)
        {
            int dañoEscudo =
                Mathf.Min(
                    datos.vidaEscudo,
                    dañoRestante
                );

            datos.vidaEscudo -=
                dañoEscudo;

            dañoRestante -=
                dañoEscudo;
        }

        if (dañoRestante <= 0)
            return;

        if (datos != null &&
            datos.vidaArmadura > 0)
        {
            int dañoArmadura =
                Mathf.Min(
                    datos.vidaArmadura,
                    dañoRestante
                );

            datos.vidaArmadura -=
                dañoArmadura;

            dañoRestante -=
                dañoArmadura;
        }

        if (dañoRestante <= 0)
            return;

        vida -= dañoRestante;

        if (vida <= 0)
            Morir();
    }

    public void Ralentizar(
        float duracion)
    {
        if (muerto ||
            datos == null ||
            !datos.puedeSerRalentizado)
        {
            return;
        }

        ralentizado = true;

        temporizadorEstado =
            Mathf.Max(
                temporizadorEstado,
                duracion
            );
    }

    public void Congelar(
        float duracion)
    {
        if (muerto ||
            datos == null ||
            !datos.puedeSerCongelado)
        {
            return;
        }

        congelado = true;

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

        aturdido = true;

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

        congelado = false;
        ralentizado = false;
        aturdido = false;
    }

    private void Morir()
    {
        if (muerto)
            return;

        muerto = true;
        activo = false;

        plantaObjetivo = null;

        if (ZombieManager.Instancia != null)
        {
            ZombieManager.Instancia.NotificarMuerte(
                this
            );
        }

        Destroy(gameObject);
    }

    public override void Kill()
    {
        Morir();
    }

    public bool EstaVivo()
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