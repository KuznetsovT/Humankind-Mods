using System;
using Amplitude.Framework;
using Amplitude.Framework.Session;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using Amplitude.Mercury.UI;
using HarmonyLib;
using UnityEngine;
using Amplitude.UI.Renderers;
using MajorEmpire = Amplitude.Mercury.Simulation.MajorEmpire;

namespace HumankindCityRenameMod
{
    // =========================================================================
    // 1. UI Тултипы для игрока-человека и фикс переполнения текста
    // =========================================================================
    [HarmonyPatch(typeof(NarrativeChoiceItem), nameof(NarrativeChoiceItem.Bind))]
    public static class Patch_NarrativeChoiceItem_Bind
    {
        [HarmonyPostfix]
        public static void Postfix(NarrativeChoiceItem __instance, short choiceIndex, RequestNarrativeEventDetails eventDetails)
        {
            if (eventDetails == null) return;

            MajorEmpire localEmpire = null;
            if (Sandbox.MajorEmpires != null && SandboxManager.Sandbox != null)
            {
                int localIndex = SandboxManager.Sandbox.LocalEmpireIndex;
                if (localIndex >= 0 && localIndex < Sandbox.MajorEmpires.Length)
                {
                    localEmpire = Sandbox.MajorEmpires[localIndex];
                }
            }

            if (localEmpire == null) return;

            string cacheKey = $"{eventDetails.NarrativeEventGUID}_{choiceIndex}";

            if (!CityRenameHelper.PendingRenames.ContainsKey(cacheKey))
            {
                int mismatchedCount = CityRenameHelper.CountMismatchedCities(localEmpire);
                float rollChance = CityRenameHelper.CalculateDynamicRenameChance(localEmpire, mismatchedCount);
                float roll = UnityEngine.Random.value;

                if (roll <= rollChance)
                {
                    var data = CityRenameHelper.CreateRenameData(localEmpire, cityIndex: choiceIndex, nameIndex: choiceIndex);
                    if (data != null)
                    {
                        CityRenameHelper.PendingRenames[cacheKey] = data;
                    }
                }
            }

            if (CityRenameHelper.PendingRenames.TryGetValue(cacheKey, out var renameData))
            {
                if (__instance.description != null)
                {
                    __instance.description.AutoAdjustHeight = true;
                    __instance.description.WordWrap = true;
                    __instance.description.InterLineAdditionalSpacing = 2;
                    __instance.description.Text += $"{renameData.GetTooltipText()}";
                    Traverse.Create(__instance.description).Method("AdjustSizesIfNecessary").GetValue();
                }
            }
        }
    }

    // =========================================================================
    // 2. Исполнение выбора в событии (Человек из кэша UI, ИИ по ходу игры)
    // =========================================================================
    [HarmonyPatch(typeof(NarrativeEventManager), nameof(NarrativeEventManager.MakeNarrativeEventChoice))]
    public static class Patch_NarrativeEventManager_MakeNarrativeEventChoice
    {
        [HarmonyPostfix]
        public static void Postfix(NarrativeEvent narrativeEvent, short choice)
        {
            if (narrativeEvent?.MajorEmpire == null) return;

            var sessionService = Services.GetService<ISessionService>();
            if (sessionService?.Session != null && !sessionService.Session.IsHosting)
            {
                return;
            }

            var empire = narrativeEvent.MajorEmpire;
            int choiceIndex = (int)choice;

            if (empire.IsControlledByHuman)
            {
                string key = $"{narrativeEvent.GUID}_{choiceIndex}";
                if (CityRenameHelper.PendingRenames.TryGetValue(key, out var renameData))
                {
                    if (renameData != null)
                    {
                        CityRenameHelper.ExecuteRename(renameData);
                    }
                    CityRenameHelper.PendingRenames.Remove(key);
                }
            }
            else
            {
                TrySingleCityRenameForAI(empire, $"NarrativeChoice(Ev={narrativeEvent.GUID}, Ch={choiceIndex})");
            }
        }

        public static void TrySingleCityRenameForAI(MajorEmpire empire, string sourceContext)
        {
            int mismatchedCount = CityRenameHelper.CountMismatchedCities(empire);
            if (mismatchedCount == 0) return;

            float rollChance = CityRenameHelper.CalculateDynamicRenameChance(empire, mismatchedCount, isForAI: true);
            float roll = UnityEngine.Random.value;

            if (roll <= rollChance)
            {
                var data = CityRenameHelper.CreateRenameData(empire, cityIndex: -1, nameIndex: 0);
                if (data != null)
                {
                    CityRenameHelper.ExecuteRename(data);
                }
            }
        }
    }

    // =========================================================================
    // 3. Переименование при смене эпохи/культуры (по 1 городу, без спама)
    // =========================================================================
    [HarmonyPatch(typeof(NarrativeEventManager), "SimulationEvent_EraChanged_Internal")]
    public static class Patch_NarrativeEventManager_EraChanged
    {
        [HarmonyPostfix]
        public static void Postfix(SimulationEvent_EraChanged e)
        {
            var sessionService = Services.GetService<ISessionService>();
            if (sessionService?.Session != null && !sessionService.Session.IsHosting)
            {
                return;
            }

            if (Sandbox.MajorEmpires == null || e.EmpireIndex < 0 || e.EmpireIndex >= Sandbox.MajorEmpires.Length)
            {
                return;
            }

            MajorEmpire empire = Sandbox.MajorEmpires[e.EmpireIndex];
            if (empire == null) return;

            if (!empire.IsControlledByHuman)
            {
                Patch_NarrativeEventManager_MakeNarrativeEventChoice.TrySingleCityRenameForAI(
                    empire,
                    $"EraChanged(Era={e.EraIndex})"
                );
            }
        }
    }

    [HarmonyPatch(typeof(DepartmentOfTheInterior), "ValidateOrderRenameSimulationEntity")]
    public static class Patch_ValidateRename
    {
        [HarmonyPostfix]
        public static void Postfix(OrderRenameSimulationEntity order, ref bool __result, DepartmentOfTheInterior __instance)
        {
            var empire = Traverse.Create(__instance).Field("majorEmpire").GetValue<MajorEmpire>();
        }
    }

    // =========================================================================
    // 4. Улучшение читаемости и увеличение шрифта главного описания события 
    // =========================================================================
    [HarmonyPatch(typeof(Amplitude.Mercury.UI.NarrativeWindow), "Bind")]
    public static class Patch_NarrativeWindow_MainDescription
    {
        [HarmonyPostfix]
        public static void Postfix(Amplitude.Mercury.UI.NarrativeWindow __instance)
        {
            if (__instance == null || !__instance.gameObject.activeInHierarchy) return;
            if (__instance.GetComponentInChildren<Amplitude.Mercury.UI.NarrativeChoiceItem>() == null) return;

            var descTransform = __instance.transform.Find("Top/TopRight/EffectsScrollView/Viewport/Description");
            if (descTransform != null)
            {
                var label = descTransform.GetComponent<Amplitude.UI.Renderers.UILabel>();
                if (label != null)
                {
                    label.FontSize = 26;
                    label.AutoAdjustHeight = true;
                    label.WordWrap = true;
                    label.InterLineAdditionalSpacing = 4;

                    Traverse.Create(label).Method("AdjustSizesIfNecessary").GetValue();

                    label.enabled = false;
                    label.enabled = true;
                }
            }
        }
    }

    // =========================================================================
    // 5. Перехват базового цвета фона (убиваем прозрачность и градиент)
    // =========================================================================
    [HarmonyPatch(typeof(Amplitude.Mercury.UI.BlurBackgroundWidget), "SetupColorAtom")]
    public static class Patch_BlurBackgroundWidget_SetupColorAtom
    {
        public static void Prefix(Amplitude.Mercury.UI.BlurBackgroundWidget __instance)
        {
            if (__instance == null) return;

            var narrativeWindow = __instance.GetComponentInParent<Amplitude.Mercury.UI.NarrativeWindow>();
            if (narrativeWindow != null && narrativeWindow.GetComponentInChildren<Amplitude.Mercury.UI.NarrativeChoiceItem>() != null)
            {
                Traverse.Create(__instance).Field("color").SetValue(new UnityEngine.Color(0.18f, 0.18f, 0.24f, 1f));
                Traverse.Create(__instance).Field("colorGradientWithFadeEnd").SetValue(null);
            }
        }
    }

    // =========================================================================
    // 6. Перехват цвета размытия (убиваем прозрачность и градиент)
    // =========================================================================
    [HarmonyPatch(typeof(Amplitude.Mercury.UI.BlurBackgroundWidget), "SetupBlurAtom")]
    public static class Patch_BlurBackgroundWidget_SetupBlurAtom
    {
        public static void Prefix(Amplitude.Mercury.UI.BlurBackgroundWidget __instance)
        {
            if (__instance == null) return;

            var narrativeWindow = __instance.GetComponentInParent<Amplitude.Mercury.UI.NarrativeWindow>();
            if (narrativeWindow != null && narrativeWindow.GetComponentInChildren<Amplitude.Mercury.UI.NarrativeChoiceItem>() != null)
            {
                Traverse.Create(__instance).Field("blurColor").SetValue(new UnityEngine.Color(0.18f, 0.18f, 0.24f, 1f));
                Traverse.Create(__instance).Field("blurGradientWithFadeEnd").SetValue(null);
            }
        }
    }
}