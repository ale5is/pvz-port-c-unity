using UnityEngine;

public class PvZAnimator : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Animaciones")]
    public string idle = "Idle";
    public string caminar = "Walk";
    public string atacar = "Attack";
    public string comer = "Eat";
    public string daño = "Damage";
    public string muerte = "Death";
    public string especial = "Special";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void Idle()
    {
        Reproducir(idle);
    }

    public void Caminar()
    {
        Reproducir(caminar);
    }

    public void Atacar()
    {
        Reproducir(atacar);
    }

    public void Comer()
    {
        Reproducir(comer);
    }

    public void RecibirDaño()
    {
        Reproducir(daño);
    }

    public void Morir()
    {
        Reproducir(muerte);
    }

    public void Especial()
    {
        Reproducir(especial);
    }

    public void Reproducir(string nombre)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        animator.Play(
            nombre,
            0,
            0f
        );
    }

    public void SetBool(
        string parametro,
        bool valor)
    {
        if (animator == null)
            return;

        if (!ExisteParametro(parametro))
            return;

        animator.SetBool(
            parametro,
            valor
        );
    }

    public void SetFloat(
        string parametro,
        float valor)
    {
        if (animator == null)
            return;

        if (!ExisteParametro(parametro))
            return;

        animator.SetFloat(
            parametro,
            valor
        );
    }

    public void SetTrigger(
        string parametro)
    {
        if (animator == null)
            return;

        if (!ExisteParametro(parametro))
            return;

        animator.SetTrigger(
            parametro
        );
    }

    private bool ExisteParametro(
        string nombre)
    {
        if (animator == null ||
            string.IsNullOrEmpty(nombre))
        {
            return false;
        }

        foreach (
            AnimatorControllerParameter parametro
            in animator.parameters)
        {
            if (parametro.name == nombre)
                return true;
        }

        return false;
    }
}