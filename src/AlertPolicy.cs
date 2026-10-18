using System;
using System.Globalization;

namespace StockNotice
{
    public class AlertDeliveryDecision
    {
        public bool ShouldNotify { get; set; }
        public string SuppressionReason { get; set; }

        public AlertDeliveryDecision()
        {
            ShouldNotify = true;
            SuppressionReason = string.Empty;
        }
    }

    public static class AlertPolicy
    {
        public const string QuietHoursReason = "quiet-hours";
        public const string CooldownReason = "cooldown";

        public static AlertDeliveryDecision Evaluate(
            TriggerRecord record,
            AppSettings settings,
            DateTime previousNotifiedAt,
            DateTime chinaNow)
        {
            AlertDeliveryDecision decision = new AlertDeliveryDecision();
            if (settings == null) return decision;

            if (IsQuietTime(settings, chinaNow))
            {
                decision.ShouldNotify = false;
                decision.SuppressionReason = QuietHoursReason;
                return decision;
            }

            int cooldownSeconds = settings.AlertCooldownSeconds;
            if (cooldownSeconds > 0
                && previousNotifiedAt != DateTime.MinValue
                && chinaNow - previousNotifiedAt < TimeSpan.FromSeconds(cooldownSeconds))
            {
                decision.ShouldNotify = false;
                decision.SuppressionReason = CooldownReason;
            }

            return decision;
        }

        public static bool IsQuietTime(AppSettings settings, DateTime chinaNow)
        {
            if (settings == null || !settings.QuietHoursEnabled) return false;

            TimeSpan start;
            TimeSpan end;
            if (!TryParseClock(settings.QuietHoursStart, out start)) return false;
            if (!TryParseClock(settings.QuietHoursEnd, out end)) return false;
            if (start == end) return false;

            TimeSpan current = chinaNow.TimeOfDay;
            if (start < end)
            {
                return current >= start && current < end;
            }
            return current >= start || current < end;
        }

        public static bool TryParseClock(string value, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrEmpty(value)) return false;
            return TimeSpan.TryParseExact(
                value.Trim(),
                "hh\\:mm",
                CultureInfo.InvariantCulture,
                out result)
                && result >= TimeSpan.Zero
                && result < TimeSpan.FromDays(1);
        }
    }
}
