using System.Collections;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instancia { get; private set; }

    [System.Serializable]
    public class ZombieGroup
    {
        public ZombieData zombie;
        [Min(1)]
        public int cantidad = 1;
        [Min(0f)]
        public float retraso = 1f;
    }

    [System.Serializable]
    public class Wave
    {
        public string nombre = "Oleada";

        public ZombieGroup[] grupos;

        [Min(0f)]
        public float retrasoFinal = 5f;

        public bool bandera;
        public bool oleadaGigante;
    }

    [Header("Oleadas")]
    public Wave[] oleadas;

    [Header("Configuración")]
    public bool iniciarAutomaticamente = true;

    public float retrasoInicial = 3f;

    [Header("Estado")]
    public int oleadaActual = -1;

    public bool EnOleada =>
        ejecutando;

    private bool ejecutando;

    private Coroutine rutina;

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
            Iniciar();
    }

    public void Iniciar()
    {
        if (ejecutando)
            return;

        rutina =
            StartCoroutine(
                RutinaPrincipal()
            );
    }

    public void Detener()
    {
        ejecutando = false;

        if (rutina != null)
        {
            StopCoroutine(rutina);
            rutina = null;
        }
    }

    private IEnumerator RutinaPrincipal()
    {
        ejecutando = true;

        if (retrasoInicial > 0f)
        {
            yield return new WaitForSeconds(
                retrasoInicial
            );
        }

        for (
            int i = 0;
            i < oleadas.Length;
            i++
        )
        {
            oleadaActual = i;

            yield return StartCoroutine(
                EjecutarOleada(
                    oleadas[i]
                )
            );
        }

        ejecutando = false;
        rutina = null;
    }

    private IEnumerator EjecutarOleada(
        Wave oleada)
    {
        if (oleada == null)
            yield break;

        if (oleada.grupos == null)
            yield break;

        foreach (
            ZombieGroup grupo
            in oleada.grupos)
        {
            if (grupo == null ||
                grupo.zombie == null)
            {
                continue;
            }

            int cantidad =
                Mathf.Max(
                    1,
                    grupo.cantidad
                );

            for (
                int i = 0;
                i < cantidad;
                i++
            )
            {
                Generar(
                    grupo.zombie
                );

                if (grupo.retraso > 0f)
                {
                    yield return new WaitForSeconds(
                        grupo.retraso
                    );
                }
            }
        }

        if (oleada.retrasoFinal > 0f)
        {
            yield return new WaitForSeconds(
                oleada.retrasoFinal
            );
        }
    }

    private void Generar(
        ZombieData datos)
    {
        if (ZombieManager.Instancia == null)
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

    public void EjecutarOleada(
        int indice)
    {
        if (indice < 0 ||
            indice >= oleadas.Length)
        {
            return;
        }

        StartCoroutine(
            EjecutarOleada(
                oleadas[indice]
            )
        );
    }

    public void SiguienteOleada()
    {
        int siguiente =
            oleadaActual + 1;

        if (siguiente >= oleadas.Length)
            return;

        oleadaActual = siguiente;

        StartCoroutine(
            EjecutarOleada(
                oleadas[siguiente]
            )
        );
    }

    public bool EsUltimaOleada()
    {
        return oleadas != null &&
               oleadas.Length > 0 &&
               oleadaActual >=
               oleadas.Length - 1;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}