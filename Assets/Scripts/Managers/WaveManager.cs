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

    [Min(0f)]
    public float retrasoInicial = 3f;

    [Header("Estado")]
    [SerializeField]
    private int oleadaActual = -1;

    [SerializeField]
    private bool ejecutando;

    private Coroutine rutina;

    public int OleadaActual =>
        oleadaActual;

    public bool EnOleada =>
        ejecutando;

    public int CantidadOleadas =>
        oleadas != null
            ? oleadas.Length
            : 0;

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

        if (oleadas == null ||
            oleadas.Length == 0)
        {
            Debug.LogWarning(
                "[PvZ] WaveManager no tiene oleadas configuradas."
            );

            return;
        }

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

        StopAllCoroutines();
    }

    public void Reiniciar()
    {
        Detener();

        oleadaActual = -1;

        Iniciar();
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
            if (!ejecutando)
                yield break;

            oleadaActual = i;

            Debug.Log(
                "[PvZ] Iniciando oleada " +
                (i + 1) +
                ": " +
                ObtenerNombreOleada(
                    oleadas[i]
                )
            );

            yield return StartCoroutine(
                EjecutarOleadaInterna(
                    oleadas[i]
                )
            );
        }

        ejecutando = false;
        rutina = null;

        Debug.Log(
            "[PvZ] Todas las oleadas terminaron."
        );
    }

    private IEnumerator EjecutarOleadaInterna(
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
            if (!ejecutando)
                yield break;

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
                if (!ejecutando)
                    yield break;

                Generar(
                    grupo.zombie
                );

                if (grupo.retraso > 0f &&
                    i < cantidad - 1)
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
        if (datos == null)
            return;

        if (Board.Instancia == null)
        {
            Debug.LogError(
                "[PvZ] No existe Board."
            );

            return;
        }

        if (ZombieManager.Instancia == null)
        {
            Debug.LogError(
                "[PvZ] No existe ZombieManager."
            );

            return;
        }

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
        if (ejecutando)
        {
            Debug.LogWarning(
                "[PvZ] Ya hay una oleada ejecutándose."
            );

            return;
        }

        if (!IndiceValido(indice))
            return;

        oleadaActual = indice;

        StartCoroutine(
            EjecutarOleadaManual(
                oleadas[indice]
            )
        );
    }

    private IEnumerator EjecutarOleadaManual(
        Wave oleada)
    {
        ejecutando = true;

        yield return StartCoroutine(
            EjecutarOleadaInterna(
                oleada
            )
        );

        ejecutando = false;
    }

    public void SiguienteOleada()
    {
        int siguiente =
            oleadaActual + 1;

        if (!IndiceValido(siguiente))
        {
            Debug.Log(
                "[PvZ] No quedan más oleadas."
            );

            return;
        }

        EjecutarOleada(
            siguiente
        );
    }

    public bool EsUltimaOleada()
    {
        if (oleadas == null ||
            oleadas.Length == 0)
        {
            return false;
        }

        return oleadaActual >=
               oleadas.Length - 1;
    }

    public bool HaySiguienteOleada()
    {
        if (oleadas == null)
            return false;

        return oleadaActual + 1 <
               oleadas.Length;
    }

    private bool IndiceValido(
        int indice)
    {
        return oleadas != null &&
               indice >= 0 &&
               indice < oleadas.Length;
    }

    private string ObtenerNombreOleada(
        Wave oleada)
    {
        if (oleada == null)
            return "Desconocida";

        if (string.IsNullOrWhiteSpace(
                oleada.nombre))
        {
            return "Oleada " +
                   (oleadaActual + 1);
        }

        return oleada.nombre;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }
}