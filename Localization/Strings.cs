using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace AsusFanControlKimera.Localization
{
    internal static partial class Strings
    {
        internal const string Italian = "it";
        internal const string English = "en";
        internal const string Russian = "ru";

        private static readonly IDictionary<string, string> ItalianStrings =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "AlreadyRunning", "AsusFanControlKimera è già in esecuzione." },
                { "ErrorTitle", "Errore Kimera" },
                { "ElevationRequired", "Sono necessari i privilegi elevati.\n\n{0}" },
                { "PsExecMissingText", "PsExec.exe non è stato trovato.\n\nKimera deve essere eseguito come account SYSTEM, come il run.bat dell'AsusFanControl funzionante.\n\nPercorso atteso:\nC:\\Program Files (x86)\\AsusFanControl\\PsExec.exe" },
                { "PsExecMissingTitle", "Kimera - PsExec mancante" },
                { "LaunchSystemFailed", "Impossibile avviare Kimera come SYSTEM.\n\n{0}" },
                { "Options", "Opzioni" },
                { "SafeLimits", "Limiti sicuri (minimo 40%, massimo 99%)" },
                { "ReleaseOnExit", "Rilascia il controllo ventole all'uscita" },
                { "MinimizeToTray", "Riduci nell'area di notifica" },
                { "StartMinimized", "Avvia ridotto nell'area di notifica" },
                { "StartWithWindows", "Avvia con Windows (richiede conferma UAC)" },
                { "Debug", "Debug" },
                { "ResetSettings", "Ripristina impostazioni" },
                { "Language", "Lingua" },
                { "Italian", "Italiano" },
                { "English", "Inglese" },
                { "Russian", "Russo" },
                { "Help", "Aiuto" },
                { "About", "Informazioni" },
                { "ControlMode", "Modalità di controllo" },
                { "AsusSystemDisabled", "Sistema ASUS (controllo disattivato)" },
                { "Manual", "Manuale" },
                { "TemperatureCurve", "Curva temperatura" },
                { "ManualSpeed", "Velocità manuale" },
                { "HardwareStatus", "Stato hardware" },
                { "CpuLabel", "CPU:" },
                { "FansLabel", "Ventole:" },
                { "Refresh", "Aggiorna" },
                { "CurveHelp", "Curva ventole — doppio clic aggiunge, trascina sposta, clic destro elimina" },
                { "CurveProfile", "Profilo:" },
                { "SaveProfile", "Salva" },
                { "SaveProfileAs", "Salva come…" },
                { "RenameProfile", "Rinomina" },
                { "DeleteProfile", "Elimina" },
                { "CurrentCurve", "Curva corrente" },
                { "Apply", "Applica" },
                { "Default", "Predefinita" },
                { "Hysteresis", "Isteresi °C" },
                { "Interval", "Intervallo ms" },
                { "CurveTemperatureAxis", "Temperatura °C" },
                { "TrayOpen", "Apri" },
                { "TrayAsusSystem", "Sistema ASUS" },
                { "TrayTemperatureCurve", "Curva di temperatura" },
                { "TrayExit", "Esci" },
                { "LanguageChanged", "Lingua dell'interfaccia impostata su Italiano." },
                { "EngineWaiting", "Motore inizializzato; attendo che la tabella hardware ASUS sia pronta…" },
                { "HardwareUnavailable", "Hardware non disponibile: {0}" },
                { "HardwareInitFailedText", "Impossibile inizializzare AsusWinIO64.\n\n{0}\n\nAvvia l'applicazione come amministratore e verifica che il modello ASUS sia supportato." },
                { "HardwareUnavailableTitle", "Kimera - hardware non disponibile" },
                { "HardwareReady", "Hardware pronto: {0} ventola/e rilevata/e." },
                { "HardwareNotReady", "Hardware non pronto: {0}" },
                { "HardwareNotReadyText", "{0}\n\nChiudi eventuali altre applicazioni di controllo ASUS e riprova." },
                { "FansNotDetectedTitle", "Kimera - ventole non rilevate" },
                { "SystemControlReleased", "Controllo rilasciato al sistema ASUS." },
                { "CurveWaiting", "Curva attiva; in attesa della temperatura." },
                { "ManualStatus", "Modalità manuale: {0}% PWM." },
                { "CommandSent", "{0} Comando inviato a {1} ventola/e (duty {2}/255)." },
                { "TraySystem", "Kimera - Sistema ASUS" },
                { "FanControlError", "Errore controllo ventole: {0}" },
                { "ZeroRpmReason", "Una ventola ha restituito 0 RPM per tre letture consecutive." },
                { "InvalidTemperatureStatus", "Temperatura ASUS non valida ({0} °C), tentativo {1}/2: curva sospesa." },
                { "InvalidTemperatureReason", "Il sensore ASUS ha restituito due temperature non valide consecutive." },
                { "CurveStatus", "Curva: {0} °C → {1}% PWM." },
                { "HardwareReadError", "Errore lettura hardware ({0}/3): {1}" },
                { "HardwareReadReason", "Tre letture hardware consecutive sono fallite." },
                { "InvalidCurveTitle", "Curva non valida" },
                { "SaveProfileAsTitle", "Salva profilo curva" },
                { "RenameProfileTitle", "Rinomina profilo curva" },
                { "ProfileNamePrompt", "Nome del profilo:" },
                { "InvalidProfileName", "Inserisci un nome non vuoto di massimo {0} caratteri." },
                { "InvalidProfileTitle", "Nome profilo non valido" },
                { "DuplicateProfileName", "Esiste già un profilo chiamato \"{0}\"." },
                { "DeleteProfilePrompt", "Eliminare il profilo \"{0}\"? La curva corrente non verrà modificata." },
                { "DeleteProfileTitle", "Elimina profilo curva" },
                { "UnsavedCurrentCurvePrompt", "La curva corrente non è salvata in un profilo. Salvarla prima di caricare il profilo selezionato?" },
                { "UnsavedProfilePrompt", "Il profilo \"{0}\" contiene modifiche non salvate. Salvarle prima di cambiare profilo?" },
                { "UnsavedProfileTitle", "Modifiche al profilo" },
                { "ProfileSavedStatus", "Profilo curva \"{0}\" salvato." },
                { "ProfileDeletedStatus", "Profilo curva \"{0}\" eliminato; la curva corrente è rimasta attiva." },
                { "OK", "OK" },
                { "Cancel", "Annulla" },
                { "UnsafeLimitsText", "Disattivando i limiti sicuri sarà possibile comandare le ventole fino all'1%. Valori troppo bassi possono arrestare fisicamente una ventola.\n\nContinuare?" },
                { "UnsafeLimitsTitle", "Kimera - impostazione potenzialmente pericolosa" },
                { "ReleaseDisabledText", "Disattivando questa opzione, la chiusura normale di Kimera non restituirà il controllo delle ventole al firmware ASUS. L'ultimo test mode/PWM può rimanere attivo dopo l'uscita.\n\nContinuare?" },
                { "ReleaseDisabledTitle", "Kimera - controllo ventole attivo dopo l'uscita" },
                { "InteractiveProfileUnavailable", "Il profilo dell'utente interattivo non è disponibile." },
                { "StartupChangeFailed", "Impossibile modificare l'avvio automatico:\n{0}" },
                { "DebugEnabled", "Debug attivo: {0}" },
                { "DebugDisabled", "Debug disattivato." },
                { "ResetPrompt", "Ripristinare tutte le impostazioni?" },
                { "AboutText", "Asus Fan Control Kimera\nVersione {0}\n\nMotore compatibile con AsusFanControl.\nUI e gestione curva reimplementate." },
                { "FailSafeReleasedStatus", "FAIL-SAFE: controllo restituito al firmware ASUS. {0}" },
                { "FailSafeUnconfirmedStatus", "FAIL-SAFE: rilascio al firmware non confermato. {0}" },
                { "Snapshot", "temperatura={0} °C; {1}; PWM={2}%; duty={3}/255" },
                { "UnexpectedError", "Errore imprevisto: {0}" },
                { "FailSafeTitle", "Kimera - FAIL-SAFE attivato" },
                { "FailSafeReleased", "Controllo restituito al firmware ASUS" },
                { "FailSafeUnconfirmed", "ATTENZIONE: rilascio al firmware non confermato" },
                { "FailSafeDetails", "Ora: {0:yyyy-MM-dd HH:mm:ss}\r\n\r\nCausa: {1}\r\n\r\nVentola/e coinvolta/e: {2}\r\n\r\nUltimi valori: {3}\r\n\r\nRilascio al firmware: {4}" },
                { "Succeeded", "RIUSCITO" },
                { "NotConfirmed", "NON CONFERMATO" },
                { "Acknowledge", "Ho compreso" },
                { "OpenLog", "Apri registro" },
                { "OpenLogFailed", "Impossibile aprire il registro" },
                { "CurveEmpty", "La curva non può essere vuota." },
                { "CurveFormat", "Formato curva non valido. Usa temperatura,percentuale separati da trattini." },
                { "CurveTemperatureRange", "Le temperature devono essere comprese tra 20 e 105 °C." },
                { "CurveSpeedRange", "Le velocità devono essere comprese tra 1 e 100%." },
                { "CurveMinimumPoints", "La curva deve contenere almeno due punti." },
                { "CurveDuplicateTemperature", "Ogni temperatura può comparire una sola volta." },
                { "CurveDecreasingSpeed", "La velocità non può diminuire all'aumentare della temperatura." },
                { "DriverFanCount", "Il driver ASUS non ha restituito un conteggio ventole valido (1-8) dopo 20 tentativi." },
                { "LogViewerTitle", "Kimera - registro diagnostico" },
                { "LogReadFailed", "Impossibile leggere il registro: {0}" },
                { "Close", "Chiudi" },
                { "RestartFailed", "Impostazioni ripristinate, ma non è stato possibile riavviare Kimera:\n{0}\n\nAvvialo manualmente." },
                { "UnprotectedLocationTitle", "Kimera - cartella non protetta" },
                { "UnprotectedLocationText", "Kimera viene eseguito come SYSTEM, ma questi percorsi possono essere modificati anche da account non amministratori:\n\n{0}\n\nUn programma eseguito senza privilegi potrebbe sostituire questi file e ottenere il controllo completo del PC. Si consiglia di spostare Kimera in una cartella protetta, ad esempio C:\\Program Files\\AsusFanControlKimera.\n\nAvviare comunque da questa posizione? La scelta verrà ricordata per questa cartella." }
            };

        private static readonly IDictionary<string, string> EnglishStrings =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "AlreadyRunning", "AsusFanControlKimera is already running." },
                { "ErrorTitle", "Kimera Error" },
                { "ElevationRequired", "Elevated privileges are required.\n\n{0}" },
                { "PsExecMissingText", "PsExec.exe was not found.\n\nKimera must run under the SYSTEM account, like the run.bat file of the working AsusFanControl installation.\n\nExpected path:\nC:\\Program Files (x86)\\AsusFanControl\\PsExec.exe" },
                { "PsExecMissingTitle", "Kimera - PsExec missing" },
                { "LaunchSystemFailed", "Kimera could not be started as SYSTEM.\n\n{0}" },
                { "Options", "Options" },
                { "SafeLimits", "Safe limits (minimum 40%, maximum 99%)" },
                { "ReleaseOnExit", "Release fan control on exit" },
                { "MinimizeToTray", "Minimize to notification area" },
                { "StartMinimized", "Start minimized in notification area" },
                { "StartWithWindows", "Start with Windows (requires UAC confirmation)" },
                { "Debug", "Debug" },
                { "ResetSettings", "Reset settings" },
                { "Language", "Language" },
                { "Italian", "Italian" },
                { "English", "English" },
                { "Russian", "Russian" },
                { "Help", "Help" },
                { "About", "About" },
                { "ControlMode", "Control mode" },
                { "AsusSystemDisabled", "ASUS System (control disabled)" },
                { "Manual", "Manual" },
                { "TemperatureCurve", "Temperature curve" },
                { "ManualSpeed", "Manual speed" },
                { "HardwareStatus", "Hardware status" },
                { "CpuLabel", "CPU:" },
                { "FansLabel", "Fans:" },
                { "Refresh", "Refresh" },
                { "CurveHelp", "Fan curve — double-click to add, drag to move, right-click to delete" },
                { "CurveProfile", "Profile:" },
                { "SaveProfile", "Save" },
                { "SaveProfileAs", "Save as…" },
                { "RenameProfile", "Rename" },
                { "DeleteProfile", "Delete" },
                { "CurrentCurve", "Current curve" },
                { "Apply", "Apply" },
                { "Default", "Default" },
                { "Hysteresis", "Hysteresis °C" },
                { "Interval", "Interval ms" },
                { "CurveTemperatureAxis", "Temperature °C" },
                { "TrayOpen", "Open" },
                { "TrayAsusSystem", "ASUS System" },
                { "TrayTemperatureCurve", "Temperature curve" },
                { "TrayExit", "Exit" },
                { "LanguageChanged", "Interface language set to English." },
                { "EngineWaiting", "Engine initialized; waiting for the ASUS hardware table to become ready…" },
                { "HardwareUnavailable", "Hardware unavailable: {0}" },
                { "HardwareInitFailedText", "AsusWinIO64 could not be initialized.\n\n{0}\n\nRun the application as administrator and verify that your ASUS model is supported." },
                { "HardwareUnavailableTitle", "Kimera - hardware unavailable" },
                { "HardwareReady", "Hardware ready: {0} fan(s) detected." },
                { "HardwareNotReady", "Hardware not ready: {0}" },
                { "HardwareNotReadyText", "{0}\n\nClose any other ASUS control applications and try again." },
                { "FansNotDetectedTitle", "Kimera - fans not detected" },
                { "SystemControlReleased", "Control released to the ASUS system." },
                { "CurveWaiting", "Curve active; waiting for temperature." },
                { "ManualStatus", "Manual mode: {0}% PWM." },
                { "CommandSent", "{0} Command sent to {1} fan(s) (duty {2}/255)." },
                { "TraySystem", "Kimera - ASUS System" },
                { "FanControlError", "Fan control error: {0}" },
                { "ZeroRpmReason", "A fan returned 0 RPM for three consecutive readings." },
                { "InvalidTemperatureStatus", "Invalid ASUS temperature ({0} °C), attempt {1}/2: curve suspended." },
                { "InvalidTemperatureReason", "The ASUS sensor returned two consecutive invalid temperatures." },
                { "CurveStatus", "Curve: {0} °C → {1}% PWM." },
                { "HardwareReadError", "Hardware read error ({0}/3): {1}" },
                { "HardwareReadReason", "Three consecutive hardware readings failed." },
                { "InvalidCurveTitle", "Invalid curve" },
                { "SaveProfileAsTitle", "Save curve profile" },
                { "RenameProfileTitle", "Rename curve profile" },
                { "ProfileNamePrompt", "Profile name:" },
                { "InvalidProfileName", "Enter a non-empty name containing at most {0} characters." },
                { "InvalidProfileTitle", "Invalid profile name" },
                { "DuplicateProfileName", "A profile named \"{0}\" already exists." },
                { "DeleteProfilePrompt", "Delete profile \"{0}\"? The current curve will not be changed." },
                { "DeleteProfileTitle", "Delete curve profile" },
                { "UnsavedCurrentCurvePrompt", "The current curve has not been saved as a profile. Save it before loading the selected profile?" },
                { "UnsavedProfilePrompt", "Profile \"{0}\" contains unsaved changes. Save them before switching profiles?" },
                { "UnsavedProfileTitle", "Profile changes" },
                { "ProfileSavedStatus", "Curve profile \"{0}\" saved." },
                { "ProfileDeletedStatus", "Curve profile \"{0}\" deleted; the current curve remains active." },
                { "OK", "OK" },
                { "Cancel", "Cancel" },
                { "UnsafeLimitsText", "Disabling safe limits will allow the fans to be set as low as 1%. Values that are too low can physically stop a fan.\n\nContinue?" },
                { "UnsafeLimitsTitle", "Kimera - potentially dangerous setting" },
                { "ReleaseDisabledText", "If this option is disabled, closing Kimera normally will not return fan control to the ASUS firmware. The last test mode/PWM value may remain active after exit.\n\nContinue?" },
                { "ReleaseDisabledTitle", "Kimera - fan control active after exit" },
                { "InteractiveProfileUnavailable", "The interactive user's profile is unavailable." },
                { "StartupChangeFailed", "Unable to change automatic startup:\n{0}" },
                { "DebugEnabled", "Debug enabled: {0}" },
                { "DebugDisabled", "Debug disabled." },
                { "ResetPrompt", "Reset all settings?" },
                { "AboutText", "Asus Fan Control Kimera\nVersion {0}\n\nEngine compatible with AsusFanControl.\nUI and curve management reimplemented." },
                { "FailSafeReleasedStatus", "FAIL-SAFE: control returned to the ASUS firmware. {0}" },
                { "FailSafeUnconfirmedStatus", "FAIL-SAFE: release to firmware was not confirmed. {0}" },
                { "Snapshot", "temperature={0} °C; {1}; PWM={2}%; duty={3}/255" },
                { "UnexpectedError", "Unexpected error: {0}" },
                { "FailSafeTitle", "Kimera - FAIL-SAFE activated" },
                { "FailSafeReleased", "Control returned to the ASUS firmware" },
                { "FailSafeUnconfirmed", "WARNING: release to firmware was not confirmed" },
                { "FailSafeDetails", "Time: {0:yyyy-MM-dd HH:mm:ss}\r\n\r\nCause: {1}\r\n\r\nAffected fan(s): {2}\r\n\r\nLatest values: {3}\r\n\r\nFirmware release: {4}" },
                { "Succeeded", "SUCCEEDED" },
                { "NotConfirmed", "NOT CONFIRMED" },
                { "Acknowledge", "I understand" },
                { "OpenLog", "Open log" },
                { "OpenLogFailed", "Unable to open the log" },
                { "CurveEmpty", "The curve cannot be empty." },
                { "CurveFormat", "Invalid curve format. Use temperature,percentage pairs separated by hyphens." },
                { "CurveTemperatureRange", "Temperatures must be between 20 and 105 °C." },
                { "CurveSpeedRange", "Speeds must be between 1 and 100%." },
                { "CurveMinimumPoints", "The curve must contain at least two points." },
                { "CurveDuplicateTemperature", "Each temperature may appear only once." },
                { "CurveDecreasingSpeed", "Speed cannot decrease as temperature increases." },
                { "DriverFanCount", "The ASUS driver did not return a valid fan count (1-8) after 20 attempts." },
                { "LogViewerTitle", "Kimera - diagnostic log" },
                { "LogReadFailed", "Unable to read the log: {0}" },
                { "Close", "Close" },
                { "RestartFailed", "Settings were reset, but Kimera could not be restarted:\n{0}\n\nPlease start it manually." },
                { "UnprotectedLocationTitle", "Kimera - unprotected folder" },
                { "UnprotectedLocationText", "Kimera runs as SYSTEM, but these paths can also be modified by non-administrator accounts:\n\n{0}\n\nA program running without privileges could replace these files and take full control of the PC. Moving Kimera to a protected folder, such as C:\\Program Files\\AsusFanControlKimera, is recommended.\n\nStart anyway from this location? Your choice will be remembered for this folder." }
            };

        private static string currentLanguage = Italian;

        internal static string CurrentLanguage { get { return currentLanguage; } }

        internal static void SetLanguage(string language)
        {
            currentLanguage = string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
                ? English
                : string.Equals(language, Russian, StringComparison.OrdinalIgnoreCase)
                    ? Russian
                    : Italian;
            CultureInfo uiCulture = CultureInfo.GetCultureInfo(currentLanguage);
            Thread.CurrentThread.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        }

        internal static string Get(string key)
        {
            string value;
            IDictionary<string, string> selected = currentLanguage == English
                ? EnglishStrings
                : currentLanguage == Russian ? RussianStrings : ItalianStrings;
            if (selected.TryGetValue(key, out value))
                return value;
            if (ItalianStrings.TryGetValue(key, out value))
                return value;
            return key;
        }

        internal static string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
        }
    }
}
