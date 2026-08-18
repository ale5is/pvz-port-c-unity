using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public static ZombieSpawner Instancia { get; private set; }

    [Header("Zombie")]
    public ZombieData[] zombies;

    [Header("Generación")]
    public float intervaloInicial = 5f;
    public float intervaloMinimo = 2f;
    public float reduccionIntervalo = 0.1f;

    private float temporizador;
    private float intervaloActual;

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
        intervaloActual = intervaloInicial;
        temporizador = intervaloInicial;
    }

    private void Update()
    {
        if (ZombieManager.Instancia == null)
            return;

        if (zombies == null ||
            zombies.Length == 0)
            return;

        temporizador -= Time.deltaTime;

        if (temporizador > 0f)
            return;

        CrearZombie();

        intervaloActual =
            Mathf.Max(
                intervaloMinimo,
                intervaloActual -
                reduccionIntervalo
            );

        temporizador = intervaloActual;
    }

    private void CrearZombie()
    {
        ZombieData datos =
            ObtenerZombieAleatorio();

        if (datos == null)
            return;

        int fila =
            Random.Range(
                0,
                Board.FILAS
            );

        ZombieManager.Instancia.CrearZombie(
            datos,
            fila
        );
    }

    private ZombieData ObtenerZombieAleatorio()
    {
        if (zombies == null ||
            zombies.Length == 0)
        {
            return null;
        }

        int indice =
            Random.Range(
                0,
                zombies.Length
            );

        return zombies[indice];
    }

    public void Reiniciar()
    {
        intervaloActual = intervaloInicial;
        temporizador = intervaloInicial;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}