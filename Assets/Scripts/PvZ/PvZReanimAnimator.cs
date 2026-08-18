// Assets/Scripts/PvZ/PvZReanimAnimator.cs

using System.Collections;
using UnityEngine;

public class PvZReanimAnimator : MonoBehaviour
{
    [Header("REANIM")]
    public string reanimNombre;

    [Header("Animator")]
    public Animator animator;

    [Header("Estado")]
    [SerializeField]
    private string animacionActual;

    [SerializeField]
    private bool recursoCargado;

    private bool inicializado;

    private void Awake()
    {
        BuscarAnimator();
    }

    private IEnumerator Start()
    {
        yield return Inicializar();
    }

    private void BuscarAnimator()
    {
        if (animator != null)
            return;

        animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
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

        CargarReanim();
    }

    public bool CargarReanim()
    {
        recursoCargado = false;

        if (PvZResourceManager.Instancia == null)
            return false;

        if (!PvZResourceManager.Instancia.EstaListo)
            return false;

        if (string.IsNullOrWhiteSpace(reanimNombre))
            return false;

        if (!PvZResourceManager.Instancia.Existe(reanimNombre))
            return false;

        recursoCargado = true;
        return true;
    }

    public void ConfigurarReanim(string nombre)
    {
        reanimNombre = nombre;
        recursoCargado = false;

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

        BuscarAnimator();

        if (animator == null)
            return;

        animator.Play(
            nombreAnimacion,
            0,
            0f
        );
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

    public Animator ObtenerAnimator()
    {
        return animator;
    }
}