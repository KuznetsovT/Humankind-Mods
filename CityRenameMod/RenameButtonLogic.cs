using Amplitude.Framework.Input;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using Amplitude.Mercury.UI;
using Amplitude.Mercury.UI.Helpers;
using Amplitude.Mercury.UI.Tooltips;
using Amplitude.UI;
using Amplitude.UI.Interactables;
using Amplitude.UI.Renderers;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace HumankindCityRenameMod
{
    public static class ModLogger
    {
        public static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CityRenameMod");
        public static void Log(string msg) => Logger?.LogInfo(msg);
        public static void Warn(string msg) => Logger?.LogWarning(msg);
        public static void Error(string msg) => Logger?.LogError(msg);
    }

    [HarmonyPatch]
    public static class RenameDebugPatches
    {
        private static FieldInfo _cancelBtnField = typeof(RenameEntityComponent).GetField("cancelRenameButton", BindingFlags.Instance | BindingFlags.NonPublic);

        // Патч: предотвращаем аварийный сброс при клике по нашей кнопке
        [HarmonyPatch(typeof(RenameEntityComponent), "RenameTextField_FocusLoss")]
        [HarmonyPrefix]
        public static bool RenameTextField_FocusLoss_Prefix(RenameEntityComponent __instance)
        {
            var holder = __instance.GetComponent<CultureRenameButtonHolder>();
            if (holder?.SuggestButton != null && InputUtils.IsMouseButtonDown(MouseButton.Left))
            {
                Vector2 mousePos = InputUtils.GetUIGlobalMousePosition();
                // Если кликнули по кнопке культуры — запрещаем FocusLoss гасить окно
                if (holder.SuggestButton.Contains(mousePos))
                {
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(SettlementScreen_HeaderPanel), "Amplitude.Mercury.UI.ISettlementScreenPanel.Bind")]
        [HarmonyPostfix]
        public static void SettlementScreen_Bind_Postfix(SettlementScreen_HeaderPanel __instance, SettlementCursorSnapshot.Data settlementData)
        {
            if (__instance?.renameCityButton?.UITransform == null) return;

            int localEmpire = SandboxManager.Sandbox != null ? SandboxManager.Sandbox.LocalEmpireIndex : -1;
            bool isOwnCity = settlementData.EmpireIndex == localEmpire && settlementData.SettlementStatus == SettlementStatuses.City;

            if (isOwnCity)
            {
                __instance.renameCityButton.UITransform.VisibleSelf = true;
                __instance.renameCityButton.UITransform.InteractiveSelf = true;
            }
        }

        [HarmonyPatch(typeof(RenameEntityComponent), nameof(RenameEntityComponent.PostLoad))]
        [HarmonyPostfix]
        public static void RenameEntityComponent_PostLoad_Postfix(RenameEntityComponent __instance)
        {
            if (__instance == null) return;
            if (!__instance.gameObject.name.Contains("Settlement")) return;

            var cancelBtnField = typeof(RenameEntityComponent).GetField("cancelRenameButton", BindingFlags.Instance | BindingFlags.NonPublic);
            UIButton cancelBtn = cancelBtnField?.GetValue(__instance) as UIButton;
            if (cancelBtn == null) return;

            var holder = __instance.GetComponent<CultureRenameButtonHolder>() ?? __instance.gameObject.AddComponent<CultureRenameButtonHolder>();

            Transform parentContainer = cancelBtn.transform.parent;
            Transform existing = parentContainer.Find("SuggestCulturalNameButton");
            if (existing != null)
            {
                holder.SuggestButton = existing.GetComponent<UIButton>();
                return;
            }

            GameObject newButtonGo = UnityEngine.Object.Instantiate(cancelBtn.gameObject, parentContainer);
            newButtonGo.name = "SuggestCulturalNameButton";

            UIButton suggestBtn = newButtonGo.GetComponent<UIButton>();
            if (suggestBtn != null)
            {
                suggestBtn.LoadIfNecessary();
                suggestBtn.LeftClick -= holder.OnButtonClick;
                suggestBtn.LeftClick += holder.OnButtonClick;
                holder.SuggestButton = suggestBtn;

                UITooltip tooltip = newButtonGo.GetComponent<UITooltip>() ?? newButtonGo.AddComponent<UITooltip>();
                TitleAndDescription tooltipData = new TitleAndDescription
                {
                    Title = ModLocalization.Get("TooltipTitle"),
                    Description = ModLocalization.Get("TooltipDesc")
                };
                tooltip.Bind(TooltipUtils.TitleAndDescription, tooltipData, null);
                tooltip.Bind(TooltipUtils.TitleAndDescription, tooltipData, null);
            }
        }

        [HarmonyPatch(typeof(RenameEntityComponent), nameof(RenameEntityComponent.UpdateTextFieldVisibilityAndFocus))]
        [HarmonyPostfix]
        public static void UpdateVisibility_Postfix(RenameEntityComponent __instance, bool visibility)
        {
            var holder = __instance?.GetComponent<CultureRenameButtonHolder>();
            if (holder?.SuggestButton?.UITransform == null) return;

            holder.SuggestButton.UITransform.VisibleSelf = visibility;

            if (visibility)
            {
                var cancelBtnField = typeof(RenameEntityComponent).GetField("cancelRenameButton", BindingFlags.Instance | BindingFlags.NonPublic);
                UIButton cancelBtn = cancelBtnField?.GetValue(__instance) as UIButton;

                if (cancelBtn != null && cancelBtn.UITransform != null)
                {
                    var cancelUi = cancelBtn.UITransform;
                    var newUi = holder.SuggestButton.UITransform;

                    // Ширина кнопки (если еще 0, берем стандартный размер иконки 24px)
                    float width = cancelUi.Width > 0f ? cancelUi.Width : 24f;

                    // Сдвигаем строго вправо от кнопки отмены
                    newUi.X = cancelUi.X + width + 6f;
                    newUi.Y = cancelUi.Y;

                    // Синхронизируем Unity Transform на случай, если UITransform использует локальные координаты
                    Vector3 cPos = cancelBtn.transform.localPosition;
                    holder.SuggestButton.transform.localPosition = new Vector3(cPos.x + width + 6f, cPos.y, cPos.z);
                }
            }
        }


        [HarmonyPatch(typeof(RenameEntityComponent), nameof(RenameEntityComponent.Bind))]
        [HarmonyPostfix]
        public static void RenameEntityComponent_Bind_Postfix(RenameEntityComponent __instance, UIButton startRenameButton)
        {
            if (__instance == null || startRenameButton == null) return;

            var holder = __instance.GetComponent<CultureRenameButtonHolder>();
            if (holder?.SuggestButton == null) return;

            // Находим картинку на исходной кнопке переименования [ A_ ]
            UIAbstractImage sourcePicto = startRenameButton.GetComponentInChildren<UIAbstractImage>(true);
            // Находим картинку на нашей кастомной кнопке
            UIAbstractImage targetPicto = holder.SuggestButton.GetComponentInChildren<UIAbstractImage>(true);

            if (sourcePicto != null && targetPicto != null)
            {
                Transform parent = targetPicto.transform.parent;
                GameObject newIcon = UnityEngine.Object.Instantiate(sourcePicto.gameObject, parent);
                newIcon.name = "Icon";
                UnityEngine.Object.Destroy(targetPicto.gameObject);
            }
        }

    }
}