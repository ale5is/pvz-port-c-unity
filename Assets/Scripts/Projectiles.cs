using UnityEngine;

public class Projectiles : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 8f;

    [Header("Daño")]
    public int daño = 20;

    [Header("Fila")]
    public int fila;

    [Header("Propiedades")]
    public bool atraviesa;
    public bool dañoEnArea;
    public float radioDaño = 1f;
    public bool ralentiza;
    public float multiplicadorRalentizacion = 0.4f;

    private bool impacto;

    public void Inicializar(
        int row,
        int damage,
        float speed)
    {
        fila = row;
        daño = damage;
        velocidad = speed;
        impacto = false;
    }

    private void Update()
    {
        if (impacto)
            return;

        transform.position +=
            Vector3.right *
            velocidad *
            Time.deltaTime;

        BuscarImpacto();
        ComprobarLimite();
    }

    private void BuscarImpacto()
    {
        if (ZombieManager.Instancia == null)
            return;

        Zombie objetivo = null;
        float distanciaMinima =
            float.MaxValue;

        foreach (
            Zombie zombie
            in ZombieManager.Instancia.ZombiesActivos)
        {
            if (zombie == null ||
                zombie.Muerto ||
                !zombie.activo ||
                zombie.fila != fila)
            {
                continue;
            }

            float diferencia =
                zombie.transform.position.x -
                transform.position.x;

            if (diferencia < -0.1f)
                continue;

            float distancia =
                Mathf.Abs(diferencia);

            if (distancia > 0.45f)
                continue;

            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                objetivo = zombie;
            }
        }

        if (objetivo == null)
            return;

        Impactar(objetivo);
    }

    private void Impactar(Zombie objetivo)
    {
        if (objetivo == null ||
            impacto)
        {
            return;
        }

        impacto = true;

        if (dañoEnArea)
        {
            AplicarDañoEnArea(
                objetivo.transform.position
            );
        }
        else
        {
            objetivo.RecibirDaño(daño);
        }

        Destroy(gameObject);
    }

    private void AplicarDañoEnArea(
        Vector3 centro)
    {
        if (ZombieManager.Instancia == null)
            return;

        float radio =
            Mathf.Max(
                0.1f,
                radioDaño
            );

        foreach (
            Zombie zombie
            in ZombieManager.Instancia.ZombiesActivos)
        {
            if (zombie == null ||
                zombie.Muerto ||
                zombie.fila != fila)
            {
                continue;
            }

            float distancia =
                Mathf.Abs(
                    zombie.transform.position.x -
                    centro.x
                );

            if (distancia <= radio)
                zombie.RecibirDaño(daño);
        }
    }

    private void ComprobarLimite()
    {
        if (Board.Instancia == null)
            return;

        float limite =
            Board.Instancia.origen.x +
            Board.Instancia.anchoCelda *
            Board.COLUMNAS +
            3f;

        if (transform.position.x > limite)
            Destroy(gameObject);
    }
}