using System.Collections;
using UnityEngine;

public class PvZReanimAnimator : MonoBehaviour
{
    public string reanimNombre;

    public PvZReanimRenderer rendererReanim;

    [SerializeField]
    private string animacionActual;

    [SerializeField]
    private bool recursoCargado;

    private bool inicializado;

    private void Awake()
    {
        BuscarRenderer();
    }

    private IEnumerator Start()
    {
        yield return Inicializar();
    }

    private void BuscarRenderer()
    {
        if (rendererReanim != null)
            return;

        rendererReanim =
            GetComponent<PvZReanimRenderer>();

        if (rendererReanim == null)
        {
            rendererReanim =
                GetComponentInChildren<PvZReanimRenderer>();
        }
    }

    public IEnumerator Inicializar()
    {
        if (inicializado)
            yield break;

        inicializado = true;

        while (PvZResourceManager.Instancia == null)
            yield return null;

        while (!PvZResourceManager.Instancia.EstaListo)
            yield return null;

        if (string.IsNullOrWhiteSpace(reanimNombre))
            yield break;

        ConfigurarRenderer();
    }

    private void ConfigurarRenderer()
    {
        BuscarRenderer();

        if (rendererReanim == null)
            return;

        rendererReanim.enabled = true;
    }

    public bool CargarReanim()
    {
        recursoCargado = false;

        if (PvZResourceManager.Instancia == null ||
            !PvZResourceManager.Instancia.EstaListo ||
            string.IsNullOrWhiteSpace(reanimNombre))
        {
            return false;
        }

        if (!PvZResourceManager.Instancia.Existe(reanimNombre))
            return false;

        ConfigurarRenderer();

        recursoCargado = true;

        return true;
    }

    public void ConfigurarReanim(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return;

        reanimNombre = nombre;

        recursoCargado = false;

        ConfigurarRenderer();

        if (PvZResourceManager.Instancia != null &&
            PvZResourceManager.Instancia.EstaListo)
        {
            CargarReanim();
        }
    }

    public void Reproducir(string nombreAnimacion)
    {
        if (string.IsNullOrWhiteSpace(nombreAnimacion))
            return;

        animacionActual = nombreAnimacion;

        BuscarRenderer();

        if (rendererReanim == null)
            return;

        rendererReanim.Reproducir();
    }

    public void Idle(string nombre)
    {
        Reproducir(nombre);
    }

    public void Caminar(string nombre)
    {
        Reproducir(nombre);
    }

    public void Atacar(string nombre)
    {
        Reproducir(nombre);
    }

    public void Comer(string nombre)
    {
        Reproducir(nombre);
    }

    public void Morir(string nombre)
    {
        Reproducir(nombre);
    }

    public void Especial(string nombre)
    {
        Reproducir(nombre);
    }

    public void Pausar()
    {
        BuscarRenderer();

        if (rendererReanim != null)
            rendererReanim.Pausar();
    }

    public void Continuar()
    {
        BuscarRenderer();

        if (rendererReanim != null)
            rendererReanim.Reproducir();
    }

    public void Reiniciar()
    {
        BuscarRenderer();

        if (rendererReanim != null)
            rendererReanim.Reiniciar();
    }

    public bool EstaCargado()
    {
        return recursoCargado;
    }

    public string ObtenerAnimacionActual()
    {
        return animacionActual;
    }

    public string ObtenerReanim()
    {
        return reanimNombre;
    }

    public PvZReanimRenderer ObtenerRenderer()
    {
        return rendererReanim;
    }
}