using System.Collections.Generic;
using UnityEngine;

public class Projectiles : MonoBehaviour
{
    [Header("Movimiento")]
    [Min(0f)]
    public float velocidad = 8f;

    [Header("Daño")]
    [Min(0)]
    public int daño = 20;

    [Header("Fila")]
    public int fila;

    [Header("Propiedades")]
    public bool atraviesa;
    public bool dañoEnArea;

    [Min(0.1f)]
    public float radioDaño = 1f;

    [Header("Efectos")]
    public bool ralentiza;

    [Range(0.05f, 1f)]
    public float multiplicadorRalentizacion = 0.4f;

    [Min(0f)]
    public float duracionRalentizacion = 3f;

    public bool congela;

    [Min(0f)]
    public float duracionCongelacion = 2f;

    public bool aturde;

    [Min(0f)]
    public float duracionAturdimiento = 1f;

    private bool impacto;

    private readonly HashSet<Zombie> zombiesImpactados =
        new HashSet<Zombie>();

    public void Inicializar(
        int row,
        int damage,
        float speed)
    {
        fila = row;
        daño = Mathf.Max(0, damage);
        velocidad = Mathf.Max(0f, speed);
        impacto = false;

        zombiesImpactados.Clear();
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

            if (zombiesImpactados.Contains(zombie))
                continue;

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

    private void Impactar(
        Zombie objetivo)
    {
        if (objetivo == null ||
            impacto)
        {
            return;
        }

        zombiesImpactados.Add(
            objetivo
        );

        if (dañoEnArea)
        {
            AplicarDañoEnArea(
                objetivo.transform.position
            );
        }
        else
        {
            AplicarEfectos(
                objetivo
            );
        }

        if (!atraviesa)
        {
            impacto = true;

            Destroy(
                gameObject
            );
        }
    }

    private void AplicarEfectos(
        Zombie zombie)
    {
        if (zombie == null ||
            zombie.Muerto)
        {
            return;
        }

        if (daño > 0)
        {
            zombie.RecibirDaño(
                daño
            );
        }

        if (ralentiza)
        {
            zombie.Ralentizar(
                duracionRalentizacion
            );
        }

        if (congela)
        {
            zombie.Congelar(
                duracionCongelacion
            );
        }

        if (aturde)
        {
            zombie.Aturdir(
                duracionAturdimiento
            );
        }
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
                !zombie.activo ||
                zombie.fila != fila)
            {
                continue;
            }

            if (zombiesImpactados.Contains(zombie))
                continue;

            float distancia =
                Mathf.Abs(
                    zombie.transform.position.x -
                    centro.x
                );

            if (distancia <= radio)
            {
                zombiesImpactados.Add(
                    zombie
                );

                AplicarEfectos(
                    zombie
                );
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
            3f;

        if (transform.position.x > limite)
        {
            Destroy(
                gameObject
            );
        }
    }

    private void OnDestroy()
    {
        zombiesImpactados.Clear();
    }
}