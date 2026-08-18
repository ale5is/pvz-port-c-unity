using System.Collections;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public static ZombieSpawner Instancia { get; private set; }

    [Header("Zombies disponibles")]
    public ZombieData[] zombiesDisponibles;

    [Header("Configuración")]
    [Min(0.1f)]
    public float intervaloSpawn = 5f;

    [Min(0f)]
    public float tiempoEntreOleadas = 15f;

    public bool iniciarAutomaticamente = true;

    [Header("Oleadas")]
    [Min(1)]
    public int zombiesIniciales = 3;

    [Min(0)]
    public int zombiesExtraPorOleada = 1;

    [Header("Estado")]
    [SerializeField]
    private int oleadaActual;

    [SerializeField]
    private bool generando;

    public int OleadaActual =>
        oleadaActual;

    public bool Generando =>
        generando;

    private Coroutine rutinaSpawner;

    private void Awake()
    {
        if (Instancia != null &&
            Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    private void Start()
    {
        if (iniciarAutomaticamente)
            IniciarSpawner();
    }

    public void IniciarSpawner()
    {
        if (generando)
            return;

        generando = true;

        rutinaSpawner =
            StartCoroutine(
                RutinaOleadas()
            );
    }

    public void DetenerSpawner()
    {
        generando = false;

        if (rutinaSpawner != null)
        {
            StopCoroutine(
                rutinaSpawner
            );

            rutinaSpawner = null;
        }
    }

    public void ReiniciarSpawner()
    {
        DetenerSpawner();

        oleadaActual = 0;

        IniciarSpawner();
    }

    private IEnumerator RutinaOleadas()
    {
        while (generando)
        {
            oleadaActual++;

            yield return StartCoroutine(
                GenerarOleada(
                    oleadaActual
                )
            );

            if (!generando)
                yield break;

            if (tiempoEntreOleadas > 0f)
            {
                yield return new WaitForSeconds(
                    tiempoEntreOleadas
                );
            }
        }
    }

    private IEnumerator GenerarOleada(
        int numeroOleada)
    {
        int cantidad =
            zombiesIniciales +
            (
                (numeroOleada - 1) *
                zombiesExtraPorOleada
            );

        cantidad =
            Mathf.Max(
                1,
                cantidad
            );

        Debug.Log(
            "[PvZ] Comenzando oleada " +
            numeroOleada +
            " - Zombies: " +
            cantidad
        );

        for (
            int i = 0;
            i < cantidad;
            i++
        )
        {
            if (!generando)
                yield break;

            GenerarZombieAleatorio();

            if (intervaloSpawn > 0f)
            {
                yield return new WaitForSeconds(
                    intervaloSpawn
                );
            }
        }
    }

    public Zombie GenerarZombieAleatorio()
    {
        if (zombiesDisponibles == null ||
            zombiesDisponibles.Length == 0)
        {
            Debug.LogWarning(
                "[PvZ] No hay ZombieData configurados."
            );

            return null;
        }

        ZombieData datos =
            ObtenerZombieAleatorioValido();

        if (datos == null)
            return null;

        return GenerarZombie(
            datos
        );
    }

    private ZombieData ObtenerZombieAleatorioValido()
    {
        int intentos =
            zombiesDisponibles.Length;

        for (
            int i = 0;
            i < intentos;
            i++
        )
        {
            ZombieData datos =
                zombiesDisponibles[
                    Random.Range(
                        0,
                        zombiesDisponibles.Length
                    )
                ];

            if (datos != null &&
                datos.prefab != null)
            {
                return datos;
            }
        }

        Debug.LogWarning(
            "[PvZ] No hay ZombieData válidos."
        );

        return null;
    }

    public Zombie GenerarZombie(
        ZombieData datos)
    {
        if (datos == null)
            return null;

        if (ZombieManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ] No existe ZombieManager."
            );

            return null;
        }

        if (Board.Instancia == null)
        {
            Debug.LogError(
                "[PvZ] No existe Board."
            );

            return null;
        }

        int fila =
            Random.Range(
                0,
                Board.FILAS
            );

        return GenerarZombie(
            datos,
            fila
        );
    }

    public Zombie GenerarZombie(
        ZombieData datos,
        int fila)
    {
        if (datos == null)
            return null;

        if (ZombieManager.Instancia == null)
            return null;

        if (Board.Instancia == null)
            return null;

        if (!Board.Instancia.EsFilaValida(fila))
        {
            Debug.LogWarning(
                "[PvZ] Fila inválida: " +
                fila
            );

            return null;
        }

        return ZombieManager.Instancia.CrearZombie(
            datos,
            fila
        );
    }

    public void GenerarCantidad(
        ZombieData datos,
        int cantidad)
    {
        if (datos == null ||
            cantidad <= 0)
        {
            return;
        }

        StartCoroutine(
            GenerarCantidadRutina(
                datos,
                cantidad
            )
        );
    }

    private IEnumerator GenerarCantidadRutina(
        ZombieData datos,
        int cantidad)
    {
        for (
            int i = 0;
            i < cantidad;
            i++
        )
        {
            GenerarZombie(
                datos
            );

            if (intervaloSpawn > 0f)
            {
                yield return new WaitForSeconds(
                    intervaloSpawn
                );
            }
        }
    }

    public void IniciarOleada()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(
            GenerarOleadaManual()
        );
    }

    private IEnumerator GenerarOleadaManual()
    {
        oleadaActual++;

        int cantidad =
            zombiesIniciales +
            (
                (oleadaActual - 1) *
                zombiesExtraPorOleada
            );

        cantidad =
            Mathf.Max(
                1,
                cantidad
            );

        for (
            int i = 0;
            i < cantidad;
            i++
        )
        {
            GenerarZombieAleatorio();

            if (intervaloSpawn > 0f)
            {
                yield return new WaitForSeconds(
                    intervaloSpawn
                );
            }
        }
    }

    public void DetenerTodasLasOleadas()
    {
        StopAllCoroutines();

        generando = false;
        rutinaSpawner = null;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}