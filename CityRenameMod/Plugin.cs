using BepInEx;
using HarmonyLib;
using System;
using UnityEngine.SceneManagement;

namespace HumankindCityRenameMod
{
    [BepInPlugin("com.username.humankind.cityrenamemod", "City Rename Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ModLogger.Log("[Plugin.Awake] Инициализация City Rename Mod v1.0.0...");

            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;

                Harmony harmony = new Harmony("com.username.humankind.cityrenamemod");
                harmony.PatchAll();

                ModLogger.Log("[Plugin.Awake] Все патчи Harmony успешно применены!");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[Plugin.Awake] Критическая ошибка при инициализации: {ex}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CityRenameHelper.PendingRenames.Clear();
            ModLogger.Log($"[Plugin] Сцена '{scene.name}' загружена. Кэш переименований очищен.");
        }
    }
}