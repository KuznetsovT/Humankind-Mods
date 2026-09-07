using System;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.UI;
using Amplitude.Mercury.UI.Helpers;
using Amplitude.Mercury.UI.Windows;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace HumankindWarTurnSilenceMod
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.humankind.warturnsilence";
        public const string PLUGIN_NAME = "War Turn Silence Mod";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} initialized and patched.");
        }
    }

    public static class WarHelper
    {
        private static bool lastReportedWarState = false;

        public static bool IsLocalPlayerAtWar()
        {
            try
            {
                var diplomaticSnapshot = Snapshots.DiplomaticSnapshot;
                if (diplomaticSnapshot == null)
                    return false;

                ref var summary = ref diplomaticSnapshot.PresentationData.LocalEmpireDiplomaticSummary;
                var summaries = summary.RelationSummaries;
                if (summaries == null)
                    return false;

                int ownerIndex = summary.OwnerEmpireIndex;

                for (int i = 0; i < summaries.Length; i++)
                {
                    if (i == ownerIndex)
                        continue;

                    ref var relation = ref summaries[i];
                    if (relation.CurrentState == DiplomaticStateType.War ||
                        relation.FeedbackedStateType == DiplomaticStateType.War)
                    {
                        if (!lastReportedWarState)
                        {
                            Plugin.Log.LogInfo($"[WarHelper] Local empire {ownerIndex} is AT WAR with empire {i}. Suppression active.");
                            lastReportedWarState = true;
                        }
                        return true;
                    }
                }

                if (lastReportedWarState)
                {
                    Plugin.Log.LogInfo($"[WarHelper] Local empire {ownerIndex} is now at peace. Suppression deactivated.");
                    lastReportedWarState = false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[WarHelper] Exception during war check: {ex.Message}");
                return false;
            }

            return false;
        }
    }

    /*[HarmonyPatch(typeof(NotificationBanner))]
    public static class NotificationBannerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("TryExpandNextUnreadItem")]
        public static bool Prefix_TryExpandNextUnreadItem(ref bool __result)
        {
            // Блокируем автоматическое раскрытие и прыжки камеры, но оставляем саму ленту рабочей
            if (WarHelper.IsLocalPlayerAtWar())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }*/

    [HarmonyPatch(typeof(IntrusivesController))]
    public static class IntrusivesControllerPatch
    {
        private static bool wasSuppressedLastCheck = false;

        [HarmonyPrefix]
        [HarmonyPatch("CanShowAnyIntrusive", new[] { typeof(WindowsSharedData) })]
        public static bool Prefix_CanShowAnyIntrusive(IntrusivesController __instance, ref bool __result)
        {
            if (WarHelper.IsLocalPlayerAtWar())
            {
                // Перехватываем новые уведомления и помечаем автоматические как "прочитанные" для авто-очереди
                SuppressModalPopups(__instance);

                if (!wasSuppressedLastCheck)
                {
                    Plugin.Log.LogInfo("[IntrusivesController] War active: Suppressing automatic modals, manual clicks allowed.");
                    wasSuppressedLastCheck = true;
                }

                // ВАЖНО: Мы НЕ возвращаем false!
                // Позволяем оригинальному методу работать, чтобы ручные клики (displayRequest) обрабатывались игрой.
                return true;
            }

            wasSuppressedLastCheck = false;
            return true;
        }

        private static void SuppressModalPopups(IntrusivesController controller)
        {
            try
            {
                var tr = Traverse.Create(controller);

                // УБРАНО: tr.Field("displayRequest").SetValue(IntrusiveData.Invalid);
                // Мы больше не затираем ручные запросы на показ окон!

                var allDataField = tr.Field("allIntrusiveData");
                if (!allDataField.FieldExists())
                    return;

                var dataArray = allDataField.Field("Data").GetValue() as IntrusiveData[];
                if (dataArray == null)
                    return;

                int length = dataArray.Length;

                for (int i = 0; i < length; i++)
                {
                    // "Обманываем" авто-очередь игры. Если уведомление автоматическое, 
                    // делаем вид, что оно уже показано. 
                    // Но так как мы не трогаем ручные запросы, по клику оно все равно откроется.
                    if (!dataArray[i].HasBeenShown && dataArray[i].IsDisplayAutomatic)
                    {
                        dataArray[i].HasBeenShown = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SuppressModalPopups] Error: {ex.Message}");
            }
        }
    }
}