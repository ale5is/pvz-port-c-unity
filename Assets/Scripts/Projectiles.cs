using UnityEngine;

public class Projectiles : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 8f;

    [Header("Daño")]
    public int daño = 20;

    [Header("Fila")]
    public int fila;

    public void Inicializar(
        int row,
        int damage,
        float speed)
    {
        fila = row;
        daño = damage;
        velocidad = speed;
    }

    private void Update()
    {
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

            if (zombie.transform.position.x <
                transform.position.x)
            {
                continue;
            }

            float distancia =
                Mathf.Abs(
                    transform.position.x -
                    zombie.transform.position.x
                );

            if (distancia <= 0.3f)
            {
                zombie.RecibirDaño(daño);

                Destroy(gameObject);

                return;
            }
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
            2f;

        if (transform.position.x > limite)
            Destroy(gameObject);
    }
}