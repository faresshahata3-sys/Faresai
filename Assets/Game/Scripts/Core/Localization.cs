using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreetRush
{
    /// <summary>
    /// Tiny two-language localization (English / Arabic), persisted in PlayerPrefs.
    /// <c>T(key)</c> returns display-ready text: Arabic strings are shaped and
    /// reordered by <see cref="ArabicShaper"/> so uGUI renders them correctly.
    /// </summary>
    public static class Localization
    {
        public static event Action<string> LanguageChanged;
        public static string Current { get; private set; } = "en";
        public static bool IsRtl => Current == "ar";

        const string PrefKey = "language";

        static readonly Dictionary<string, (string en, string ar)> Strings = new Dictionary<string, (string, string)>
        {
            { "menu_subtitle", ("Open World, for Mobile", "عالم مفتوح - للموبايل") },
            { "btn_new_game", ("NEW GAME", "لعبة جديدة") },
            { "btn_continue", ("CONTINUE", "استمرار") },
            { "btn_settings", ("SETTINGS", "الإعدادات") },
            { "btn_exit", ("EXIT", "خروج") },
            { "continue_no_save", ("NO SAVED GAME YET", "لا يوجد حفظ بعد") },
            { "settings_title", ("SETTINGS", "الإعدادات") },
            { "settings_language", ("LANGUAGE", "اللغة") },
            { "settings_graphics", ("GRAPHICS", "الجرافيكس") },
            { "gfx_low", ("LOW", "منخفض") },
            { "gfx_high", ("HIGH", "عالي") },
            { "gfx_ultra", ("ULTRA", "فائق") },
            { "settings_back", ("BACK", "رجوع") },
            { "hud_health", ("HEALTH", "الصحة") },
            { "hud_speed_unit", ("km/h", "كم/س") },
            { "hud_busted", ("BUSTED", "تم القبض عليك") },
            { "hud_pause", ("PAUSED", "إيقاف مؤقت") },
            { "hud_resume", ("RESUME", "استكمال") },
            { "hud_main_menu", ("MAIN MENU", "القائمة الرئيسية") },
            { "loading", ("LOADING MIAMI...", "جاري تحميل ميامي...") },
        };

        public static void Load()
        {
            string lang = PlayerPrefs.GetString(PrefKey, "en");
            Current = lang == "ar" ? "ar" : "en";
        }

        public static void SetLanguage(string lang)
        {
            if (lang != "en" && lang != "ar") return;
            if (lang == Current) return;
            Current = lang;
            PlayerPrefs.SetString(PrefKey, lang);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke(lang);
        }

        /// <summary>Raw (logical order) translated string.</summary>
        public static string Raw(string key)
        {
            if (!Strings.TryGetValue(key, out var entry)) return key;
            return Current == "ar" ? entry.ar : entry.en;
        }

        /// <summary>Display-ready translated string.</summary>
        public static string T(string key) => Display(Raw(key));

        /// <summary>Makes any (possibly Arabic) text display-ready for uGUI.</summary>
        public static string Display(string text) => ArabicShaper.Shape(text);
    }
}
