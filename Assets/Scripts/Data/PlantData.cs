using UnityEngine;

[CreateAssetMenu(fileName = "PlantData", menuName = "PvZ/Plant")]
public class PlantData : ScriptableObject
{
    [Header("General")]
    public PlantType tipo;
    public string nombre;

    [Header("Economía")]
    public int costo = 100;
    public float recarga = 7.5f;

    [Header("Vida")]
    public int vida = 300;

    [Header("Combate")]
    public int daño = 20;
    public float velocidadAtaque = 1.5f;

    [Header("Prefab")]
    public Plant prefab;

    [Header("Icono")]
    public Sprite icono;
}