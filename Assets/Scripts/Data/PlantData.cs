using UnityEngine;

[CreateAssetMenu(
    fileName = "PlantData",
    menuName = "PvZ/Plant Data"
)]
public class PlantData : ScriptableObject
{
    [Header("Identificación")]
    public string nombre = "Planta";

    [Header("Prefab")]
    public Plant prefab;

    [Header("Coste")]
    public int costo = 100;

    [Header("Recarga")]
    public float recarga = 7.5f;

    [Header("Vida")]
    public int vida = 300;

    [Header("Combate")]
    public int daño = 20;
    public float intervaloAtaque = 1.5f;
    public float rangoAtaque = 10f;

    [Header("Producción")]
    public int produccionSol = 25;
    public float intervaloProduccion = 24f;
}