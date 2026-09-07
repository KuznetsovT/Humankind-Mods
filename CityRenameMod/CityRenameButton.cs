using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using Amplitude.Mercury.UI;
using Amplitude.UI;
using Amplitude.UI.Interactables;
using System;
using System.Reflection;
using UnityEngine;
using MajorEmpire = Amplitude.Mercury.Simulation.MajorEmpire;

namespace HumankindCityRenameMod
{
    public class CultureRenameButtonHolder : MonoBehaviour
    {
        public RenameEntityComponent Component;
        public UIButton SuggestButton;

        // Настоящие приватные поля и методы из RenameEntityComponent
        private static MethodInfo _sendOrderMethod = typeof(RenameEntityComponent).GetMethod(
            "SendOrderRenameSimulationEntity",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static FieldInfo _guidField = typeof(RenameEntityComponent).GetField(
            "simulationEntityGUID",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private void Awake()
        {
            this.Component = this.GetComponent<RenameEntityComponent>();
        }

        public void OnButtonClick(IUIButton button)
        {
            if (this.Component == null) return;

            var sandbox = SandboxManager.Sandbox;
            if (sandbox == null || Sandbox.MajorEmpires == null || Sandbox.SettlementNamesAncillary == null)
                return;

            int localIndex = sandbox.LocalEmpireIndex;
            if (localIndex < 0 || localIndex >= Sandbox.MajorEmpires.Length)
                return;

            MajorEmpire localEmpire = Sandbox.MajorEmpires[localIndex];
            if (localEmpire == null)
                return;

            // Генерируем культурное имя
            EntityNameInfo nameInfo = default(EntityNameInfo);
            Sandbox.SettlementNamesAncillary.SetSettlementNameInfo(localEmpire, ref nameInfo);
            string suggested = nameInfo.GetDefaultName();

            if (string.IsNullOrEmpty(suggested)) return;

            ModLogger.Log($"[CultureRenameButtonHolder] Отправляем переименование: '{suggested}'");

            // Достаем GUID текущего города
            SimulationEntityGUID targetGuid = SimulationEntityGUID.Zero;
            if (_guidField != null)
            {
                targetGuid = (SimulationEntityGUID)_guidField.GetValue(this.Component);
            }

            // Вызываем нативную отправку приказа
            if (_sendOrderMethod != null && targetGuid != SimulationEntityGUID.Zero)
            {
                _sendOrderMethod.Invoke(this.Component, new object[] { targetGuid, suggested });
            }
            else
            {
                // Запасной прямой путь, если рефлексия не сработала
                SandboxManager.PostAndTrackOrder(new OrderRenameSimulationEntity
                {
                    SimulationEntityGUID = targetGuid,
                    Name = suggested
                });
                this.Component.UpdateTextFieldVisibilityAndFocus(false);
            }
        }
    }
}