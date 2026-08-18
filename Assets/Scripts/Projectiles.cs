using UnityEngine;

public class Projectiles : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 8f;

    [Header("Daño")]
    public int daño = 20;

    [Header("Objetivo")]
    public int fila;

    private void Update()
    {
        transform.position +=
            Vector3.right *
            velocidad *
            Time.deltaTime;

        ComprobarZombie();
    }

    public void Inicializar(
        int row,
        int damage,
        float speed)
    {
        fila = row;
        daño = damage;
        velocidad = speed;
    }

    private void ComprobarZombie()
    {
        if (ZombieManager.Instancia == null)
            return;

        foreach (Zombie zombie in ZombieManager.Instancia.ZombiesActivos)
        {
            if (zombie == null ||
                zombie.Muerto ||
                zombie.fila != fila)
                continue;

            if (Mathf.Abs(
                    transform.position.x -
                    zombie.transform.position.x) <= 0.3f)
            {
                zombie.RecibirDaño(daño);
                Destroy(gameObject);
                return;
            }
        }

        if (Board.Instancia != null)
        {
            float limite =
                Board.Instancia.origen.x +
                Board.Instancia.anchoCelda *
                Board.COLUMNAS +
                2f;

            if (transform.position.x > limite)
                Destroy(gameObject);
        }
    }
}