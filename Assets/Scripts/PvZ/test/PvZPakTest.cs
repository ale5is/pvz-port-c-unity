using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PvZReanim
{
    public class PvZPakTest : MonoBehaviour
    {
        [SerializeField]
        private string pakFileName = "main.pak";

        private void Start()
        {
            string path =
                Path.Combine(
                    Application.streamingAssetsPath,
                    "PvZ",
                    pakFileName
                );

            Debug.Log(
                "[PvZPakTest] Buscando:\n" +
                path
            );

            PvZPakReader reader =
                new PvZPakReader();

            if (!reader.Load(path))
            {
                Debug.LogError(
                    "[PvZPakTest] " +
                    "No se pudo cargar main.pak."
                );

                return;
            }

            Debug.Log(
                "[PvZPakTest] PAK OK | " +
                "Archivos: " +
                reader.FileCount
            );

            List<string> results =
                reader.Find(
                    "Peashooter"
                );

            Debug.Log(
                "[PvZPakTest] Resultados " +
                "para Peashooter: " +
                results.Count
            );

            for (int i = 0;
                 i < results.Count;
                 i++)
            {
                Debug.Log(
                    "[PvZPakTest] " +
                    results[i]
                );
            }
        }
    }
}