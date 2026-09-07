using Amplitude;
using Amplitude.Framework;
using Amplitude.Mercury;
using Amplitude.Mercury.Data.Presentation;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Presentation;
using Amplitude.Mercury.UI;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EmblematicStripesMod
{
    [BepInPlugin("com.humankind.emblematicstripes", "Emblematic Stripes Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;
            try
            {
                new Harmony("com.humankind.emblematicstripes").PatchAll();
                Log.LogInfo("[INIT] Emblematic Quaters Colored Mod v1.0.0 loaded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[INIT] Fatal error during patch: {ex}");
            }
        }
    }

    public static class NativeFeedbackManager
    {
        private struct DistrictRecord
        {
            public int TileIndex;
            public Color DistrictColor;
            public string ConstructibleName;
        }

        private static readonly Dictionary<int, DistrictRecord> RegisteredDistricts = new Dictionary<int, DistrictRecord>();
        private static bool isButtonVisible = true;
        private static bool isCameraLayerVisible = true;

        private static int currentlyHoveredTile = -1;

        public static bool IsPlacingSpecialDistrict = false;

        // Изолированный список только для Заповедников
        private static List<int> reserveSuppressedTiles = new List<int>();

        public static void SetReserveSuppressedTiles(List<int> tiles)
        {
            // СНАЧАЛА сохраняем старый список и перезаписываем текущий на новый (пустой при отмене).
            var oldTiles = reserveSuppressedTiles;
            reserveSuppressedTiles = tiles;

            // ТЕПЕРЬ даем команду на отрисовку.
            // Мод увидит, что reserveSuppressedTiles уже пуст, и послушно вернет цвета.
            foreach (int oldTile in oldTiles)
            {
                if (!tiles.Contains(oldTile) && RegisteredDistricts.TryGetValue(oldTile, out var rec))
                {
                    ApplyRecord(rec);
                }
            }

            // Глушим цвета на новых клетках (при начале постройки)
            foreach (int newTile in tiles)
            {
                if (!oldTiles.Contains(newTile) && RegisteredDistricts.TryGetValue(newTile, out var rec))
                {
                    RemoveRecord(rec);
                }
            }
        }

        public static void SetHoveredTile(int tileIndex)
        {
            if (currentlyHoveredTile == tileIndex) return;

            int oldTile = currentlyHoveredTile;
            currentlyHoveredTile = tileIndex;

            if (oldTile != -1 && RegisteredDistricts.TryGetValue(oldTile, out var oldRecord))
            {
                ApplyRecord(oldRecord);
            }

            if (currentlyHoveredTile != -1 && RegisteredDistricts.TryGetValue(currentlyHoveredTile, out var newRecord))
            {
                RemoveRecord(newRecord);
            }
        }

        public static bool IsTileColored(int tileIndex)
        {
            return isButtonVisible && isCameraLayerVisible && RegisteredDistricts.ContainsKey(tileIndex);
        }

        public static bool IsTileColoredAndNotHovered(int tileIndex)
        {
            return IsTileColored(tileIndex) && tileIndex != currentlyHoveredTile;
        }

        public static void RegisterAndApply(PresentationDistrict district)
        {
            if (district == null) return;

            try
            {
                var worldPos = district.WorldPosition;
                if (worldPos == WorldPosition.Invalid) return;

                int tileIndex = worldPos.ToTileIndex();
                if (tileIndex < 0) return;

                Color finalColor = StripeColorResolver.GetDistrictColor(district);

                if (finalColor.a <= 0f)
                {
                    if (RegisteredDistricts.TryGetValue(tileIndex, out var oldRec))
                    {
                        RemoveRecord(oldRec);
                        RegisteredDistricts.Remove(tileIndex);
                    }
                    return;
                }

                if (RegisteredDistricts.TryGetValue(tileIndex, out var existingRecord))
                {
                    if (existingRecord.DistrictColor == finalColor) return;
                    RemoveRecord(existingRecord);
                }

                var record = new DistrictRecord
                {
                    TileIndex = tileIndex,
                    DistrictColor = finalColor,
                    ConstructibleName = district.ConstructibleDefinitionName.ToString()
                };

                RegisteredDistricts[tileIndex] = record;

                if (isButtonVisible && isCameraLayerVisible)
                {
                    ApplyRecord(record);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[NATIVE_FEEDBACK] Error registering feedback: {ex}");
            }
        }

        private static void ApplyRecord(DistrictRecord record)
        {
            if (!isButtonVisible || !isCameraLayerVisible) return;
            if (record.TileIndex == currentlyHoveredTile) return;

            // Заповедники: не рисуем цвет, если клетка в списке подавления
            if (IsPlacingSpecialDistrict && reserveSuppressedTiles.Contains(record.TileIndex)) return;

            var feedbackController = Presentation.PresentationTileFeedbackController;
            if (feedbackController == null) return;

            Color col = record.DistrictColor;
            col.a = 0.1f;
            StaticString frameId = new StaticString($"Mod_Emb_{record.TileIndex}");

            feedbackController.StartFeedback(
                record.TileIndex,
                0f,
                ref frameId,
                PresentationEntityLevelBuildEnums.TileFeedback.Buildable,
                col,
                PresentationTileFeedbackController.OptionsEnum.None,
                0
            );
        }

        private static void RemoveRecord(DistrictRecord record)
        {
            var feedbackController = Presentation.PresentationTileFeedbackController;
            if (feedbackController == null) return;

            StaticString frameId = new StaticString($"Mod_Emb_{record.TileIndex}");
            feedbackController.StopFeedback(record.TileIndex, ref frameId, 0f);
        }

        public static void SetButtonVisibility(bool visible)
        {
            isButtonVisible = visible;
            UpdateGlobalVisibility();
        }

        public static void SetCameraLayerVisibility(bool visible)
        {
            isCameraLayerVisible = visible;
            UpdateGlobalVisibility();
        }

        private static void UpdateGlobalVisibility()
        {
            bool show = isButtonVisible && isCameraLayerVisible;
            foreach (var rec in RegisteredDistricts.Values)
            {
                if (show) ApplyRecord(rec);
                else RemoveRecord(rec);
            }
        }
    }

    [HarmonyPatch(typeof(PresentationCameraController), "SetHexagonIconVisibility")]
    public static class HexagonIconVisibility_Patch
    {
        public static void Postfix(bool visible)
        {
            NativeFeedbackManager.SetButtonVisibility(visible);
        }
    }

    [HarmonyPatch(typeof(PresentationCameraController), "OnCameraLayerChanged")]
    public static class CameraLayerChanged_Patch
    {
        private static readonly System.Reflection.FieldInfo LayerIndexField =
            AccessTools.Field(typeof(PresentationCameraController), "currentLayerIndex");

        public static void Postfix(PresentationCameraController __instance)
        {
            if (__instance == null || LayerIndexField == null) return;
            try
            {
                int layerIndex = (int)LayerIndexField.GetValue(__instance);
                NativeFeedbackManager.SetCameraLayerVisibility(layerIndex <= 2);
            }
            catch { }
        }
    }

    public static class StripeColorResolver
    {
        private static readonly HashSet<int> KnownTrainStationTiles = new HashSet<int>();
        public static readonly HashSet<int> KnownAirportTiles = new HashSet<int>();
        private static readonly Dictionary<string, string> CachedRoles = new Dictionary<string, string>();

        private static readonly Color ColFood = new Color(0.55f, 0.70f, 0.28f);
        private static readonly Color ColIndustry = new Color(0.68f, 0.35f, 0.18f);
        private static readonly Color ColScience = new Color(0.28f, 0.62f, 0.70f);
        private static readonly Color ColMoney = new Color(0.72f, 0.66f, 0.25f);
        private static readonly Color ColCommons = new Color(0.68f, 0.32f, 0.54f);
        private static readonly Color ColGarrison = new Color(0.64f, 0.28f, 0.29f);
        private static readonly Color ColHarbor = new Color(0.23f, 0.37f, 0.50f);
        private static readonly Color ColFaith = new Color(0.73f, 0.69f, 0.59f);
        private static readonly Color ColRailway = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color ColAirport = new Color(0.75f, 0.92f, 1.00f);

        public static void MarkAsTrainStation(int tileIndex)
        {
            if (tileIndex >= 0) KnownTrainStationTiles.Add(tileIndex);
        }

        private static Color GetRoleColor(string role)
        {
            switch (role)
            {
                case "Food": return ColFood;
                case "Industry": return ColIndustry;
                case "Science": return ColScience;
                case "Money": return ColMoney;
                case "Commons": return ColCommons;
                case "Garrison": return ColGarrison;
                case "Harbor": return ColHarbor;
                case "Faith": return ColFaith;
                default: return Color.clear;
            }
        }

        public static Color GetDistrictColor(PresentationDistrict district)
        {
            if (district == null) return Color.clear;

            int tileIndex = district.WorldPosition.ToTileIndex();
            if (KnownTrainStationTiles.Contains(tileIndex)) return ColRailway;

            StaticString defName = district.ConstructibleDefinitionName;
            if (StaticString.IsNullOrEmpty(defName)) return Color.clear;

            string defNameStr = defName.ToString();

            if (defNameStr.IndexOf("Airport", StringComparison.OrdinalIgnoreCase) >= 0 &&
                defNameStr.IndexOf("Aerodrome", StringComparison.OrdinalIgnoreCase) < 0)
            {
                KnownAirportTiles.Add(tileIndex);
                return ColAirport;
            }

            if (defNameStr.IndexOf("TrainStation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defNameStr.IndexOf("Railway", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defNameStr.Equals("Extension_Base_TrainStation", StringComparison.OrdinalIgnoreCase))
            {
                KnownTrainStationTiles.Add(tileIndex);
                return ColRailway;
            }

            var database = Databases.GetDatabase<ConstructibleDefinition>(false);
            if (database == null) return Color.clear;

            ConstructibleDefinition def = database.GetValue(defName);
            if (def == null) return Color.clear;

            bool isWonder = def.ConstructibleType == ConstructibleType.ArtificialWonder;
            bool isHolySite = defNameStr.IndexOf("HolySite", StringComparison.OrdinalIgnoreCase) >= 0 || def.Category == ConstructibleCategory.Faith;

            if (!def.IsEmblematic && !isWonder && !isHolySite) return Color.clear;

            string dominantRole;
            if (CachedRoles.TryGetValue(defNameStr, out var cachedRole))
            {
                dominantRole = cachedRole;
            }
            else
            {
                dominantRole = ResolveDominantRole(def);
                CachedRoles[defNameStr] = dominantRole ?? "";
            }

            if (string.IsNullOrEmpty(dominantRole)) return Color.clear;
            if (isWonder && dominantRole != "Faith") return Color.clear;
            if (dominantRole == "Faith" && !def.IsEmblematic && !isWonder) return Color.clear;

            return GetRoleColor(dominantRole);
        }

        private static string ResolveDominantRole(ConstructibleDefinition def)
        {
            HashSet<string> fimsDescriptors = new HashSet<string>();
            HashSet<string> minorDescriptors = new HashSet<string>();

            IEnumerable descriptors = null;
            var allDescProp = def.GetType().GetProperty("AllDescriptors", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (allDescProp != null) descriptors = allDescProp.GetValue(def, null) as IEnumerable;

            if (descriptors == null)
            {
                var descField = def.GetType().GetField("OwnDescriptorReferences", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (descField != null) descriptors = descField.GetValue(def) as IEnumerable;
            }

            if (descriptors != null)
            {
                foreach (var desc in descriptors)
                {
                    if (desc == null) continue;
                    string dName = "";
                    var elNameProp = desc.GetType().GetProperty("ElementName");
                    if (elNameProp != null) dName = elNameProp.GetValue(desc, null)?.ToString() ?? "";
                    else dName = desc.ToString();

                    if (string.IsNullOrEmpty(dName)) continue;

                    if (dName.IndexOf("PublicOrderLoss", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    if (dName.Equals("Effect_Extension_Base_Food", StringComparison.OrdinalIgnoreCase)) fimsDescriptors.Add("Food");
                    else if (dName.Equals("Effect_Extension_Base_Industry", StringComparison.OrdinalIgnoreCase)) fimsDescriptors.Add("Industry");
                    else if (dName.Equals("Effect_Extension_Base_Science", StringComparison.OrdinalIgnoreCase)) fimsDescriptors.Add("Science");
                    else if (dName.Equals("Effect_Extension_Base_Money", StringComparison.OrdinalIgnoreCase)) fimsDescriptors.Add("Money");

                    else if (dName.Equals("Effect_Extension_Base_PublicOrder", StringComparison.OrdinalIgnoreCase)) minorDescriptors.Add("Commons");
                    else if (dName.Equals("Effect_Extension_Base_Military", StringComparison.OrdinalIgnoreCase)) minorDescriptors.Add("Garrison");
                    else if (dName.Equals("Tag_Extension_Religious", StringComparison.OrdinalIgnoreCase) || dName.Equals("Effect_Extension_HolySite", StringComparison.OrdinalIgnoreCase)) minorDescriptors.Add("Faith");
                }
            }

            HashSet<string> properties = new HashSet<string>();
            properties.Add(def.Category.ToString());

            var exploProp = def.GetType().GetProperty("ExploitationRuleDefinition");
            if (exploProp != null)
            {
                var exploVal = exploProp.GetValue(def, null);
                if (exploVal != null)
                {
                    string exploRule = exploVal.ToString();
                    if (exploRule.Contains("Food")) properties.Add("Food");
                    if (exploRule.Contains("Industry")) properties.Add("Industry");
                    if (exploRule.Contains("Science")) properties.Add("Science");
                    if (exploRule.Contains("Money")) properties.Add("Money");
                }
            }

            string districtType = "None";
            var dtField = def.GetType().GetField("DistrictType");
            if (dtField != null)
            {
                var val = dtField.GetValue(def);
                if (val != null) districtType = val.ToString();
            }

            if (fimsDescriptors.Count > 0)
            {
                if (fimsDescriptors.Count == 1)
                {
                    var enumerator = fimsDescriptors.GetEnumerator();
                    enumerator.MoveNext();
                    return enumerator.Current;
                }

                foreach (var role in fimsDescriptors)
                {
                    if (properties.Contains(role)) return role;
                }

                var fallback = fimsDescriptors.GetEnumerator();
                fallback.MoveNext();
                return fallback.Current;
            }

            if (minorDescriptors.Count > 0)
            {
                var enumerator = minorDescriptors.GetEnumerator();
                enumerator.MoveNext();
                return enumerator.Current;
            }

            if (districtType == "FaithDistrict") return "Faith";
            if (districtType == "PublicOrderDistrict") return "Commons";
            if (districtType == "MilitaryDistrict") return "Garrison";
            if (districtType == "Harbour") return "Harbor";
            if (districtType == "FimsDistrict")
            {
                foreach (var prop in properties)
                {
                    if (prop == "Food" || prop == "Industry" || prop == "Science" || prop == "Money") return prop;
                }
            }

            return null;
        }
    }

    [HarmonyPatch]
    public static class HideVanillaWhiteHexes_Patch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Amplitude.Mercury.Presentation.DistrictPlacementTileFeedback");
            if (type == null) return null;

            foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (method.Name == "UpdateWith" && method.GetParameters().Length == 4)
                {
                    return method;
                }
            }
            return null;
        }

        public static void Prefix(object[] __args, out List<KeyValuePair<int, object>> __state)
        {
            __state = new List<KeyValuePair<int, object>>();

            if (__args == null || __args.Length < 3) return;

            Array originalTiles = __args[0] as Array;
            if (originalTiles == null) return;

            try
            {
                int validTileCount = (int)__args[1];
                int currentPositionIndex = (int)__args[2];

                if (validTileCount <= 0)
                {
                    NativeFeedbackManager.SetHoveredTile(-1);
                    return;
                }

                var elementType = originalTiles.GetType().GetElementType();
                var tileIndexField = elementType.GetField("TileIndex") ?? elementType.GetField("Index");

                if (tileIndexField != null)
                {
                    // === ТВОЯ ИСХОДНАЯ РАБОЧАЯ ЛОГИКА ОПРЕДЕЛЕНИЯ МЫШИ ===
                    int hoveredTileIndex = -1;
                    if (currentPositionIndex >= 0 && currentPositionIndex < validTileCount)
                    {
                        object hoveredTileStruct = originalTiles.GetValue(currentPositionIndex);
                        hoveredTileIndex = (int)tileIndexField.GetValue(hoveredTileStruct);
                    }

                    NativeFeedbackManager.SetHoveredTile(hoveredTileIndex);

                    // === ИЗОЛИРОВАННАЯ ЛОГИКА ДЛЯ ЗАПОВЕДНИКОВ ===
                    if (NativeFeedbackManager.IsPlacingSpecialDistrict)
                    {
                        List<int> toSuppress = new List<int>();
                        for (int i = 0; i < validTileCount; i++)
                        {
                            object t = originalTiles.GetValue(i);
                            int idx = (int)tileIndexField.GetValue(t);
                            toSuppress.Add(idx);
                        }
                        if (hoveredTileIndex != -1) toSuppress.Add(hoveredTileIndex);

                        NativeFeedbackManager.SetReserveSuppressedTiles(toSuppress);
                        return; // Выходим, не трогая массивы
                    }

                    // Очищаем подавление, если это обычный район
                    NativeFeedbackManager.SetReserveSuppressedTiles(new List<int>());

                    // === ТВОЯ ИСХОДНАЯ ЛОГИКА ДЛЯ ОБЫЧНЫХ РАЙОНОВ (safeTile) ===
                    object safeTile = null;
                    for (int i = 0; i < validTileCount; i++)
                    {
                        object t = originalTiles.GetValue(i);
                        int idx = (int)tileIndexField.GetValue(t);
                        if (!NativeFeedbackManager.IsTileColored(idx))
                        {
                            safeTile = t;
                            break;
                        }
                    }

                    if (safeTile == null) return;

                    for (int i = 0; i < validTileCount; i++)
                    {
                        object tileStruct = originalTiles.GetValue(i);
                        int tIndex = (int)tileIndexField.GetValue(tileStruct);

                        if (NativeFeedbackManager.IsTileColoredAndNotHovered(tIndex))
                        {
                            __state.Add(new KeyValuePair<int, object>(i, tileStruct));
                            originalTiles.SetValue(safeTile, i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[HIDE_HEXES] Error in Prefix: {ex}");
            }
        }

        public static void Postfix(object[] __args, List<KeyValuePair<int, object>> __state)
        {
            if (__state == null || __state.Count == 0 || __args == null || __args.Length == 0) return;

            Array originalTiles = __args[0] as Array;
            if (originalTiles == null) return;

            try
            {
                foreach (var kvp in __state)
                {
                    originalTiles.SetValue(kvp.Value, kvp.Key);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[HIDE_HEXES] Error in Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch]
    public static class DistrictPlacementTileFeedback_Clear_Patch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Amplitude.Mercury.Presentation.DistrictPlacementTileFeedback");
            if (type == null) return null;
            return AccessTools.Method(type, "Clear");
        }

        public static void Postfix()
        {
            NativeFeedbackManager.SetHoveredTile(-1);
            NativeFeedbackManager.SetReserveSuppressedTiles(new List<int>());
        }
    }

    [HarmonyPatch(typeof(PresentationDistrict), "CreatePresentationDistrict")]
    public static class PresentationDistrict_Create_Patch
    {
        public static void Postfix(PresentationDistrict __result, ref DistrictInfo districtInfo)
        {
            if (__result == null) return;

            if (districtInfo.TrainStation)
            {
                StripeColorResolver.MarkAsTrainStation(__result.WorldPosition.ToTileIndex());
            }

            NativeFeedbackManager.RegisterAndApply(__result);
        }
    }

    [HarmonyPatch(typeof(PresentationDistrict), "UpdateFromDistrictInfo")]
    public static class PresentationDistrict_Update_Patch
    {
        public static void Postfix(PresentationDistrict __instance, ref DistrictInfo districtInfo)
        {
            if (__instance == null) return;

            if (districtInfo.TrainStation)
            {
                StripeColorResolver.MarkAsTrainStation(__instance.WorldPosition.ToTileIndex());
            }

            NativeFeedbackManager.RegisterAndApply(__instance);
        }
    }

    // === ПЕРЕХВАТ РЕЖИМА СТРОИТЕЛЬСТВА ЗАПОВЕДНИКОВ ===
    [HarmonyPatch(typeof(Amplitude.Mercury.Presentation.DistrictPlacementCursor), "Activate")]
    public static class DistrictPlacementCursor_Activate_Patch
    {
        public static void Postfix(Amplitude.Mercury.Presentation.DistrictPlacementCursor __instance)
        {
            try
            {
                var defNameObj = Traverse.Create(__instance).Property("DistrictToEvaluate").GetValue();
                if (defNameObj != null)
                {
                    string defName = defNameObj.ToString();

                    if (defName.IndexOf("Reserve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("Park", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        NativeFeedbackManager.IsPlacingSpecialDistrict = true;
                    }
                    else
                    {
                        NativeFeedbackManager.IsPlacingSpecialDistrict = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[CURSOR_ACTIVATE] Error: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Amplitude.Mercury.Presentation.DistrictPlacementCursor), "Deactivate")]
    public static class DistrictPlacementCursor_Deactivate_Patch
    {
        public static void Postfix()
        {
            NativeFeedbackManager.IsPlacingSpecialDistrict = false;
            NativeFeedbackManager.SetReserveSuppressedTiles(new List<int>());
        }
    }
}