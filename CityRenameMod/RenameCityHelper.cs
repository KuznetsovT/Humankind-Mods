using System;
using System.Collections.Generic;
using Amplitude.Framework;
using Amplitude.Framework.Localization;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using UnityEngine;
using MajorEmpire = Amplitude.Mercury.Simulation.MajorEmpire;
using Settlement = Amplitude.Mercury.Simulation.Settlement;
using Math = System.Math;

namespace HumankindCityRenameMod
{
    public class CityRenameData
    {
        public Settlement TargetCity;
        public string OldName;
        public string NewName;

        public string GetTooltipText()
        {
            return $"\n\n• {ModLocalization.GetFormatted("TooltipActionRename", OldName, NewName)}";
        }
    }

    public static class CityRenameHelper
    {
        public static Dictionary<string, CityRenameData> PendingRenames = new Dictionary<string, CityRenameData>();

        public static string SafeLocalize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                ILocalizationService service = Services.GetService<ILocalizationService>();
                if (service != null)
                {
                    return service.Localize(key);
                }
            }
            catch { }
            return key;
        }

        public static List<Settlement> GetMismatchedCities(MajorEmpire empire, HashSet<string> existingNames = null)
        {
            List<Settlement> mismatched = new List<Settlement>();

            if (empire?.FactionDefinition?.LocalizedSettlementNames == null || empire.Settlements == null)
            {
                return mismatched;
            }

            var faction = empire.FactionDefinition;

            for (int i = 0; i < empire.Settlements.Count; i++)
            {
                Settlement settlement = empire.Settlements[i];
                if (settlement == null)
                    continue;

                if (!((ISimulationEntityWithNameInfo)settlement).CanBeRenamedBy(empire))
                    continue;

                string currentName = ((ISimulationEntityWithNameInfo)settlement).GetUserGeneratedName();
                if (string.IsNullOrEmpty(currentName))
                    currentName = settlement.EntityName.ToString();

                if (existingNames != null)
                {
                    existingNames.Add(currentName);
                    existingNames.Add(SafeLocalize(currentName));
                }

                bool isCurrentCulture = false;
                for (int j = 0; j < faction.LocalizedSettlementNames.Length; j++)
                {
                    string key = faction.LocalizedSettlementNames[j];
                    if (string.Equals(currentName, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(currentName, SafeLocalize(key), StringComparison.OrdinalIgnoreCase))
                    {
                        isCurrentCulture = true;
                        break;
                    }
                }

                if (!isCurrentCulture)
                {
                    mismatched.Add(settlement);
                }
            }

            return mismatched;
        }

        public static int CountMismatchedCities(MajorEmpire empire)
        {
            return GetMismatchedCities(empire).Count;
        }

        public static float CalculateDynamicRenameChance(MajorEmpire empire, int candidateCitiesCount, bool isForAI = false)
        {
            if (empire == null || candidateCitiesCount <= 0)
                return 0f;

            float baseSingleCityWeight = isForAI ? 0.075f : 0.12f;
            float poolChance = 1f - Mathf.Pow(1f - baseSingleCityWeight, candidateCitiesCount);

            float speedMultiplier = 1f;
            try
            {
                // Используем правильный путь к эпохе через DepartmentOfDevelopment
                if (empire.DepartmentOfDevelopment != null && empire.DepartmentOfDevelopment.CurrentEraIndex >= 5)
                {
                    speedMultiplier *= 0.66f;
                }

                var sandbox = SandboxManager.Sandbox;
                if (sandbox != null)
                {
                    int turnLimit = 300;
                    var egSettingsProp = sandbox.GetType().GetProperty("EndGameSettings");
                    if (egSettingsProp != null)
                    {
                        var egSettings = egSettingsProp.GetValue(sandbox, null);
                        if (egSettings != null)
                        {
                            var turnLimitField = egSettings.GetType().GetField("TurnLimit") ?? egSettings.GetType().GetField("turnLimit");
                            if (turnLimitField != null)
                            {
                                turnLimit = Convert.ToInt32(turnLimitField.GetValue(egSettings));
                            }
                        }
                    }

                    if (turnLimit > 0)
                    {
                        float turnMult = (float)turnLimit / 300f;
                        if (turnMult > 0.05f)
                        {
                            speedMultiplier *= (1f / Mathf.Sqrt(turnMult));
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки рефлексии
            }

            return Mathf.Clamp01(poolChance * speedMultiplier);
        }

        public static CityRenameData CreateRenameData(MajorEmpire empire, int cityIndex = -1, int nameIndex = 0)
        {
            if (empire?.FactionDefinition?.LocalizedSettlementNames == null || empire.FactionDefinition.LocalizedSettlementNames.Length == 0)
                return null;

            var faction = empire.FactionDefinition;
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Settlement> mismatchedCities = GetMismatchedCities(empire, existingNames);

            if (mismatchedCities.Count == 0)
                return null;

            int selectedCityIndex = (cityIndex < 0)
                ? UnityEngine.Random.Range(0, mismatchedCities.Count)
                : Math.Abs(cityIndex) % mismatchedCities.Count;

            Settlement targetCity = mismatchedCities[selectedCityIndex];

            string oldName = ((ISimulationEntityWithNameInfo)targetCity).GetUserGeneratedName();
            if (string.IsNullOrEmpty(oldName))
                oldName = targetCity.EntityName.ToString();
            oldName = SafeLocalize(oldName);

            List<string> availableNames = new List<string>();
            for (int k = 0; k < faction.LocalizedSettlementNames.Length; k++)
            {
                string rawKey = faction.LocalizedSettlementNames[k];
                string locName = SafeLocalize(rawKey);

                if (!existingNames.Contains(locName) && !existingNames.Contains(rawKey))
                {
                    availableNames.Add(locName);
                }
            }

            if (availableNames.Count == 0)
            {
                availableNames.Add(SafeLocalize(faction.LocalizedSettlementNames[0]));
            }

            int safeNameIndex = Math.Abs(nameIndex) % availableNames.Count;
            string newName = availableNames[safeNameIndex];

            return new CityRenameData
            {
                TargetCity = targetCity,
                OldName = oldName,
                NewName = newName
            };
        }

        public static void ExecuteRename(CityRenameData data)
        {
            if (data?.TargetCity == null || string.IsNullOrEmpty(data.NewName))
                return;

            SimulationEntityGUID cityGuid = data.TargetCity.GUID;
            string newName = data.NewName.Trim();
            int ownerIndex = data.TargetCity.Empire.Entity != null ? data.TargetCity.Empire.Entity.Index : -1;

            ModLogger.Log($"[ExecuteRename] Применение переименования: GUID={cityGuid}, Empire #{ownerIndex}, '{data.OldName}' -> '{newName}'");

            try
            {
                if (data.TargetCity.Empire.Entity != null && data.TargetCity.Empire.Entity.IsControlledByHuman)
                {
                    var order = new OrderRenameSimulationEntity
                    {
                        SimulationEntityGUID = cityGuid,
                        Name = newName
                    };
                    SandboxManager.PostAndTrackOrder(order);
                    return;
                }

                ISimulationEntityWithNameInfo entityWithName = data.TargetCity as ISimulationEntityWithNameInfo;
                if (entityWithName != null)
                {
                    entityWithName.SetName(newName);
                    Sandbox.EntityNamesRepository.RequestStringVerification(cityGuid, newName);

                    object sender = data.TargetCity.Empire.Entity?.DepartmentOfTheInterior ?? (object)data.TargetCity;
                    SimulationEvent_SettlementNameChanged.Raise(sender, data.TargetCity);

                    ISimulationEntityWithSynchronization synchro = data.TargetCity as ISimulationEntityWithSynchronization;
                    if (synchro != null)
                    {
                        Sandbox.SimulationEntityRepository.SetSynchronizationDirty(synchro);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[ExecuteRename] Ошибка при переименовании: {ex}");
            }
        }
    }
}