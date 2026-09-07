using System.Collections.Generic;
using Amplitude.Framework;
using Amplitude.Framework.Localization;

namespace HumankindCityRenameMod
{
    public static class ModLocalization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _localizations = new Dictionary<string, Dictionary<string, string>>()
        {
            {
                "en", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Suggest Cultural Name" },
                    { "TooltipDesc", "Applies the next historical name of your current culture." },
                    { "TooltipActionRename", "Renames city {0} to {1}" },
                    { "LogRename", "Applied rename: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "ru", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Предложить культурное название" },
                    { "TooltipDesc", "Подставляет следующее историческое имя вашей текущей культуры." },
                    { "TooltipActionRename", "Переименовывает город {0} в {1}" },
                    { "LogRename", "Применение переименования: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "fr", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Suggérer un nom culturel" },
                    { "TooltipDesc", "Applique le nom historique suivant de votre culture actuelle." },
                    { "TooltipActionRename", "Renomme la ville {0} en {1}" },
                    { "LogRename", "Renommage appliqué : GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "de", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Kulturellen Namen vorschlagen" },
                    { "TooltipDesc", "Wendet den nächsten historischen Namen eurer aktuellen Kultur an." },
                    { "TooltipActionRename", "Benennt die Stadt {0} in {1} um" },
                    { "LogRename", "Umbenennung angewendet: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "es", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Sugerir nombre cultural" },
                    { "TooltipDesc", "Aplica el siguiente nombre histórico de tu cultura actual." },
                    { "TooltipActionRename", "Renombra la ciudad {0} a {1}" },
                    { "LogRename", "Renombramiento aplicado: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "it", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Suggerisci nome culturale" },
                    { "TooltipDesc", "Applica il successivo nome storico della tua cultura attuale." },
                    { "TooltipActionRename", "Rinomina la città {0} in {1}" },
                    { "LogRename", "Rinominazione applicata: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "pl", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Zaproponuj nazwę kulturową" },
                    { "TooltipDesc", "Stosuje kolejną historyczną nazwę twojej obecnej kultury." },
                    { "TooltipActionRename", "Zmienia nazwę miasta {0} na {1}" },
                    { "LogRename", "Zastosowano zmianę nazwy: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "pt", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Sugerir Nome Cultural" },
                    { "TooltipDesc", "Aplica o próximo nome histórico da sua cultura atual." },
                    { "TooltipActionRename", "Renomeia a cidade {0} para {1}" },
                    { "LogRename", "Renomeação aplicada: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "ko", new Dictionary<string, string>
                {
                    { "TooltipTitle", "문화적 이름 제안" },
                    { "TooltipDesc", "현재 문화의 다음 역사적 이름을 적용합니다." },
                    { "TooltipActionRename", "도시 {0}을(를) {1}(으)로 이름 변경" },
                    { "LogRename", "이름 변경 적용됨: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "tr", new Dictionary<string, string>
                {
                    { "TooltipTitle", "Kültürel İsim Öner" },
                    { "TooltipDesc", "Mevcut kültürünüzün bir sonraki tarihi ismini uygular." },
                    { "TooltipActionRename", "{0} şehri {1} olarak yeniden adlandırılır" },
                    { "LogRename", "Yeniden adlandırma uygulandı: GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "zh-cn", new Dictionary<string, string>
                {
                    { "TooltipTitle", "建议文化名称" },
                    { "TooltipDesc", "应用您当前文化的下一个历史名称。" },
                    { "TooltipActionRename", "将城市 {0} 重命名为 {1}" },
                    { "LogRename", "已应用重命名：GUID={0}, '{1}' -> '{2}'" }
                }
            },
            {
                "zh-tw", new Dictionary<string, string>
                {
                    { "TooltipTitle", "建議文化名稱" },
                    { "TooltipDesc", "應用您當前文化的下一個歷史名稱。" },
                    { "TooltipActionRename", "將城市 {0} 重新命名為 {1}" },
                    { "LogRename", "已應用重新命名：GUID={0}, '{1}' -> '{2}'" }
                }
            }
        };

        private static string GetCurrentLanguageCode()
        {
            var service = Services.GetService<ILocalizationService>();
            if (service != null && !string.IsNullOrEmpty(service.CurrentLanguage))
            {
                string lang = service.CurrentLanguage.ToLowerInvariant();
                ModLogger.Log($"[Localization] CurrentLanguage: '{lang}'");

                // Сначала ищем точное совпадение (критично для zh-cn и zh-tw)
                if (_localizations.ContainsKey(lang))
                {
                    return lang;
                }

                // Если не нашли, пробуем обрезать до двух символов
                if (lang.Length >= 2)
                {
                    string shortLang = lang.Substring(0, 2);
                    if (_localizations.ContainsKey(shortLang))
                    {
                        return shortLang;
                    }
                }
            }

            return "en"; // Фолбэк на английский по умолчанию
        }

        public static string Get(string key)
        {
            string lang = GetCurrentLanguageCode();

            if (_localizations.TryGetValue(lang, out var langDict) && langDict.TryGetValue(key, out var value))
            {
                return value;
            }

            if (_localizations["en"].TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return key;
        }

        public static string GetFormatted(string key, params object[] args)
        {
            string template = Get(key);
            return string.Format(template, args);
        }
    }
}