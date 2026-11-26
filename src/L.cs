using System;

namespace Pscht_QoL
{
    /// <summary>
    /// Sprache der Mod-Texte: Konfig „00 - Sprache / Language“ = Auto (wie Valheim), Deutsch oder English.
    /// Auto: Deutsch, wenn Valheim auf Deutsch steht, sonst Englisch.
    /// Benutzen: L.T("deutscher Text", "English text").
    /// </summary>
    internal static class L
    {
        internal const string Auto = "Auto", German = "Deutsch", English = "English";
        internal static readonly string[] Options = { Auto, German, English };

        private static bool? _autoGerman;

        /// <summary>Sind die Texte gerade deutsch?</summary>
        internal static bool De
        {
            get
            {
                string v = Cfg.Language != null ? Cfg.Language.Value : Auto;
                if (v == German) return true;
                if (v == English) return false;
                return ValheimIsGerman();
            }
        }

        private static bool ValheimIsGerman()
        {
            try
            {
                var loc = Localization.instance;
                if (loc != null) { bool g = loc.GetSelectedLanguage() == "German"; _autoGerman = g; return g; }
            }
            catch { }
            return _autoGerman ?? false;
        }

        internal static string T(string de, string en) => De ? de : en;
    }
}
