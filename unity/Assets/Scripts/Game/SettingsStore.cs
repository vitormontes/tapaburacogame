using TapaBuraco.Core;
using UnityEngine;

namespace TapaBuraco.Game
{
    /// <summary>
    /// Persistência das preferências, equivalente ao <c>localStorage["tapaburaco.v1"]</c> do protótipo.
    /// Mesma chave e mesmo contrato: se o save estiver corrompido, cai no padrão sem quebrar a partida.
    /// </summary>
    public static class SettingsStore
    {
        /// <summary>Chave usada no PlayerPrefs (a mesma do protótipo web).</summary>
        public const string Key = "tapaburaco.v1";

        /// <summary>Lê as preferências gravadas; devolve o padrão quando não há save válido.</summary>
        public static GameSettings Load()
        {
            string raw = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return new GameSettings();
            }

            GameSettings settings = null;
            try
            {
                settings = JsonUtility.FromJson<GameSettings>(raw);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SettingsStore] save ilegível, voltando ao padrão: {e.Message}");
            }

            if (settings == null)
            {
                return new GameSettings();
            }

            settings.Sanitize();
            return settings;
        }

        /// <summary>Grava as preferências. Chamado a cada mudança de opção e a cada fim de partida.</summary>
        public static void Save(GameSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(settings));
            PlayerPrefs.Save();
        }

        /// <summary>Apaga o save (útil em testes manuais no editor).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
