using System.Globalization;

namespace ClaudeUsageTray.Resources;

/// <summary>
/// Central store for every user-facing string, with English and Russian variants
/// selected from <see cref="CultureInfo.CurrentUICulture"/>. Keeping the text here
/// (rather than inline literals) makes the app translatable and removes duplication.
/// </summary>
internal static class Strings
{
    private static readonly bool Ru =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase);

    private static string Pick(string en, string ru) => Ru ? ru : en;

    // Tray menu.
    public static string OpenUsage => Pick("Open usage", "Открыть расход");
    public static string RefreshNow => Pick("Refresh now", "Обновить сейчас");
    public static string RunAtStartup => Pick("Run at Windows startup", "Запускать вместе с Windows");
    public static string Notifications => Pick("Notifications", "Уведомления");
    public static string RefreshInterval => Pick("Refresh interval", "Интервал обновления");
    public static string Theme => Pick("Theme", "Тема");
    public static string ThemeSystem => Pick("System", "Как в системе");
    public static string ThemeDark => Pick("Dark", "Тёмная");
    public static string ThemeLight => Pick("Light", "Светлая");
    public static string CheckForUpdates => Pick("Check for updates on startup", "Проверять обновления при запуске");
    public static string About => Pick("About", "О программе");
    public static string Exit => Pick("Exit", "Выход");
    public static string Minutes(int n) => Ru ? $"{n} мин" : (n == 1 ? "1 minute" : $"{n} minutes");

    // Tray tooltip / balloon.
    public static string TrayUpdating => Pick("Claude usage — updating…", "Расход Claude — обновление…");
    public static string TrayMax(int percent) => Pick($"Claude usage — {percent}% max", $"Расход Claude — {percent}% макс.");
    public static string TrayFailed => Pick("Claude usage — update failed", "Расход Claude — ошибка обновления");
    public static string BalloonTitle => Pick("Claude usage is running low", "Расход Claude приближается к лимиту");
    public static string BalloonBody(int percent) =>
        Pick($"Your highest usage window is at {percent}%.", $"Наибольшее окно расхода: {percent}%.");

    // Popup.
    public static string PopupTitle => Pick("Claude usage", "Расход Claude");
    public static string WaitingFirstUpdate => Pick("Waiting for the first update…", "Ожидание первого обновления…");
    public static string SubscriptionGeneric => Pick("Claude Code subscription", "Подписка Claude Code");
    public static string NotUpdatedYet => Pick("Not updated yet", "Ещё не обновлялось");
    public static string ContactingAnthropic => Pick("Contacting Anthropic…", "Связь с Anthropic…");
    public static string Updating => Pick("Updating…", "Обновление…");
    public static string Refresh => Pick("Refresh", "Обновить");
    public static string UpdatedAt(string time) => Pick($"Updated {time}", $"Обновлено {time}");
    public static string CredentialsSource(string source) => Pick($"Credentials: {source}", $"Источник: {source}");
    public static string CloseWindow => Pick("Close usage window", "Закрыть окно расхода");
    public static string RefreshUsageNow => Pick("Refresh usage now", "Обновить расход сейчас");
    public static string SignIn => Pick("Sign in", "Войти");
    public static string HowToSignIn =>
        Pick("Sign in with Claude Code, then Refresh.", "Войдите в Claude Code, затем нажмите «Обновить».");
    public static string ExpiresIn(string remaining) =>
        Pick($"Sign-in expires in {remaining}", $"Сессия истекает через {remaining}");
    public static string ShortHoursMinutes(int hours, int minutes) =>
        Ru ? $"{hours}ч {minutes}м" : $"{hours}h {minutes}m";
    public static string ShortMinutes(int minutes) =>
        Ru ? $"{minutes}м" : $"{minutes}m";
    public static string UpdateAvailable(string version) =>
        Pick($"Update available: {version}", $"Доступно обновление: {version}");

    // Metric row / reset countdown.
    public static string ResetUnavailable => Pick("Reset time unavailable", "Время сброса неизвестно");
    public static string ResettingNow => Pick("Resetting now", "Сбрасывается сейчас");
    public static string ResetsInDaysHours(int days, int hours) =>
        Pick($"Resets in {days}d {hours}h", $"Сброс через {days}д {hours}ч");
    public static string ResetsInHoursMinutes(int hours, int minutes) =>
        Pick($"Resets in {hours}h {minutes}m", $"Сброс через {hours}ч {minutes}м");
    public static string ResetsInMinutes(int minutes) =>
        Pick($"Resets in {minutes}m", $"Сброс через {minutes}м");
    public static string PercentAccessible(string label, int percent) =>
        Pick($"{label}: {percent} percent", $"{label}: {percent} процентов");

    // About box.
    public static string AboutText(string version) => Ru
        ? $"Claude Usage Tray {version}\n\nНеофициальный монитор расхода подписки Claude Code для области уведомлений Windows.\n\nУчётные данные остаются на этом компьютере и отправляются только в Anthropic."
        : $"Claude Usage Tray {version}\n\nAn unofficial open-source Windows monitor for Claude Code subscription usage.\n\nCredentials stay on this computer and are sent only to Anthropic.";

    // Errors (surfaced in the popup status line).
    public static string CredentialsNotFound => Pick(
        "Claude Code credentials were not found. Sign in with Claude Code (run `claude`, then `/login`), then Refresh.",
        "Учётные данные Claude Code не найдены. Войдите в Claude Code (запустите `claude`, затем `/login`) и нажмите «Обновить».");
    public static string SessionExpired => Pick(
        "Your Claude Code sign-in has expired. Reauthenticate in Claude Code (`/login`), then Refresh.",
        "Сессия Claude Code истекла. Войдите заново в Claude Code (`/login`) и нажмите «Обновить».");
    public static string HttpError(int status) =>
        Pick($"Anthropic usage service returned HTTP {status}.", $"Сервис расхода Anthropic вернул HTTP {status}.");
    public static string UnknownFormat => Pick(
        "Anthropic returned an unknown usage format.",
        "Anthropic вернул неизвестный формат данных о расходе.");
    public static string GenericUpdateFailed => Pick(
        "Update failed. See the project page for troubleshooting.",
        "Не удалось обновить. Подробности — на странице проекта.");
    public static string AlreadyRunning => Pick(
        "Claude Usage Tray is already running.",
        "Claude Usage Tray уже запущен.");
}
