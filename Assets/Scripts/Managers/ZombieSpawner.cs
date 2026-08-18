using System.Collections;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public static ZombieSpawner Instancia { get; private set; }

    [Header("Zombies disponibles")]
    public ZombieData[] zombiesDisponibles;

    [Header("Configuración")]
    [Min(0.1f)]
    public float intervaloSpawn = 10f;

    public bool iniciarAutomaticamente = true;

    [Header("Oleada")]
    public int zombiesPorOleada = 5;
    public float tiempoEntreOleadas = 20f;

    private bool generando;
    private int oleadaActual;

    public int OleadaActual =>
        oleadaActual;

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

        StartCoroutine(
            RutinaOleadas()
        );
    }

    public void DetenerSpawner()
    {
        generando = false;

        StopAllCoroutines();
    }

    private IEnumerator RutinaOleadas()
    {
        while (generando)
        {
            oleadaActual++;

            yield return StartCoroutine(
                GenerarOleada()
            );

            yield return new WaitForSeconds(
                tiempoEntreOleadas
            );
        }
    }

    private IEnumerator GenerarOleada()
    {
        int cantidad =
            Mathf.Max(
                1,
                zombiesPorOleada +
                Mathf.FloorToInt(
                    oleadaActual * 0.5f
                )
            );

        for (int i = 0;
             i < cantidad;
             i++)
        {
            GenerarZombieAleatorio();

            yield return new WaitForSeconds(
                intervaloSpawn
            );
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
            zombiesDisponibles[
                Random.Range(
                    0,
                    zombiesDisponibles.Length
                )
            ];

        return GenerarZombie(
            datos
        );
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

        int fila =
            Random.Range(
                0,
                Board.FILAS
            );

        return ZombieManager.Instancia.CrearZombie(
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
            return;

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
        for (int i = 0;
             i < cantidad;
             i++)
        {
            GenerarZombie(datos);

            yield return new WaitForSeconds(
                intervaloSpawn
            );
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
            Mathf.Max(
                1,
                zombiesPorOleada
            );

        for (int i = 0;
             i < cantidad;
             i++)
        {
            GenerarZombieAleatorio();

            yield return new WaitForSeconds(
                intervaloSpawn
            );
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}