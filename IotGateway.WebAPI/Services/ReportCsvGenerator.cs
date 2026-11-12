using System.Globalization;
using System.Text;
using IotGateway.Common.Exceptions;
using IotGateway.Domain.Entities;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Services;

internal static class ReportCsvGenerator
{
    private const int MaxSourceRows = 10_000;
    private const int MaxBytes = 8 * 1024 * 1024;

    public static async Task<byte[]> GenerateAsync(
        AppDbContext db, string reportType, DateTime start, DateTime endExclusive,
        CancellationToken cancellationToken)
    {
        var csv = new CsvWriter(cancellationToken);
        csv.Row("dataset", "period_start", "device_id", "production_line", "shift",
            "energy_type", "unit", "alarm_level", "alarm_status", "metric", "value", "notes");
        csv.Row("metadata", start, null, null, null, null, null, null, null, "report_type", reportType,
            "Only measured operational fields; no operator names, alarm messages, remarks or AI narratives.");
        csv.Row("metadata", start, null, null, null, null, null, null, null, "range_end_inclusive",
            endExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Calendar dates use stored timestamps without timezone conversion. Periods include only rows in the requested range.");
        csv.Row("metadata", start, null, null, null, null, null, null, null, "period_convention", reportType,
            "weekly: Monday-start weeks; monthly: calendar months; daily/oee/alarm: calendar days. Missing records are not zero measurements.");
        var sourceRows = 0;

        if (reportType != "alarm")
        {
            // Project only approved measurements; never load user names or free-form remarks.
            var production = await db.Set<ProductionRecord>().AsNoTracking()
                .Where(x => (x.ShiftDate ?? x.CreatedAt) >= start && (x.ShiftDate ?? x.CreatedAt) < endExclusive)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    Date = x.ShiftDate ?? x.CreatedAt, MissingShiftDate = x.ShiftDate == null,
                    x.DeviceId, x.ProductionLine, x.Shift, x.PlanQuantity,
                    x.ActualQuantity, x.DefectQuantity, x.RunDuration, x.DowntimeDuration
                })
                .Take(MaxSourceRows + 1).ToListAsync(cancellationToken);
            sourceRows += production.Count;
            CheckCount(sourceRows);
            csv.Row("metadata", start, null, null, null, null, null, null, null, "oee_limitations", null,
                "Performance and OEE unavailable: ideal cycle time and scheduled production time are not stored. Recorded availability = run / (run + downtime), not schedule-based availability. Rates are fractions, not percentages.");

            foreach (var group in production.GroupBy(x => new
                     { Period = Period(x.Date, reportType), x.DeviceId, x.ProductionLine, x.Shift })
                     .OrderBy(x => x.Key.Period).ThenBy(x => x.Key.DeviceId)
                     .ThenBy(x => x.Key.ProductionLine, StringComparer.Ordinal)
                     .ThenBy(x => x.Key.Shift, StringComparer.Ordinal))
            {
                var actual = group.Sum(x => (long)x.ActualQuantity);
                var defects = group.Sum(x => (long)x.DefectQuantity);
                var run = group.Sum(x => (long)x.RunDuration);
                var downtime = group.Sum(x => (long)x.DowntimeDuration);
                var validQuality = group.All(x => x.ActualQuantity >= 0 && x.DefectQuantity >= 0 &&
                    x.DefectQuantity <= x.ActualQuantity);
                var validAvailability = group.All(x => x.RunDuration >= 0 && x.DowntimeDuration >= 0);
                var note = group.Any(x => x.MissingShiftDate)
                    ? "Some shift dates are missing; CreatedAt was used for those records."
                    : string.Empty;
                void Metric(string name, object? value, string unit, string extra = "") =>
                    csv.Row("production", group.Key.Period, group.Key.DeviceId, group.Key.ProductionLine,
                        group.Key.Shift, null, unit, null, null, name, value, $"{note} {extra}".Trim());
                Metric("source_record_count", group.Count(), "records");
                Metric("planned_quantity", group.Sum(x => (long)x.PlanQuantity), "pieces");
                Metric("actual_quantity", actual, "pieces");
                Metric("defect_quantity", defects, "pieces");
                Metric("good_quantity", validQuality ? actual - defects : null, "pieces",
                    validQuality ? "" : "Unavailable: invalid actual/defect counts.");
                Metric("run_duration", run, "minutes");
                Metric("downtime_duration", downtime, "minutes");
                Metric("quality_rate", validQuality && actual > 0 ? (decimal)(actual - defects) / actual : null,
                    "ratio", "Good / actual; unavailable for invalid counts or zero actual production.");
                Metric("recorded_availability_rate", validAvailability && run + downtime > 0
                        ? (decimal)run / (run + downtime) : null,
                    "ratio", "Run / (run + downtime); unavailable for invalid or zero recorded durations.");
                Metric("performance_rate", null, "ratio", "Unavailable: no ideal cycle time; planned quantity is not a substitute.");
                Metric("oee_rate", null, "ratio", "Unavailable: performance and scheduled production time are not known.");
            }
        }

        if (reportType is "daily" or "weekly" or "monthly")
        {
            var energy = await db.Set<EnergyRecord>().AsNoTracking()
                .Where(x => x.RecordHour >= start && x.RecordHour < endExclusive)
                .OrderBy(x => x.Id)
                .Select(x => new { x.RecordHour, x.DeviceId, x.EnergyType, x.Unit, x.Consumption })
                .Take(MaxSourceRows - sourceRows + 1).ToListAsync(cancellationToken);
            sourceRows += energy.Count;
            CheckCount(sourceRows);
            foreach (var group in energy.GroupBy(x => new
                     { Period = Period(x.RecordHour, reportType), x.DeviceId, x.EnergyType, x.Unit })
                     .OrderBy(x => x.Key.Period).ThenBy(x => x.Key.DeviceId)
                     .ThenBy(x => x.Key.EnergyType, StringComparer.Ordinal)
                     .ThenBy(x => x.Key.Unit, StringComparer.Ordinal))
            {
                csv.Row("energy", group.Key.Period, group.Key.DeviceId, null, null, group.Key.EnergyType,
                    group.Key.Unit, null, null, "consumption", group.Sum(x => x.Consumption),
                    "Sum of recorded consumption, separated by energy type and unit.");
                csv.Row("energy", group.Key.Period, group.Key.DeviceId, null, null, group.Key.EnergyType,
                    group.Key.Unit, null, null, "source_record_count", group.Count(), null);
            }
        }

        if (reportType != "oee")
        {
            var alarms = await db.Set<Alarm>().AsNoTracking()
                .Where(x => x.TriggeredAt >= start && x.TriggeredAt < endExclusive)
                .OrderBy(x => x.Id)
                .Select(x => new { x.TriggeredAt, x.DeviceId, x.Level, x.Status })
                .Take(MaxSourceRows - sourceRows + 1).ToListAsync(cancellationToken);
            sourceRows += alarms.Count;
            CheckCount(sourceRows);
            foreach (var group in alarms.GroupBy(x => new
                     { Period = Period(x.TriggeredAt, reportType), x.DeviceId, x.Level, x.Status })
                     .OrderBy(x => x.Key.Period).ThenBy(x => x.Key.DeviceId)
                     .ThenBy(x => x.Key.Level, StringComparer.Ordinal)
                     .ThenBy(x => x.Key.Status, StringComparer.Ordinal))
                csv.Row("alarm", group.Key.Period, group.Key.DeviceId, null, null, null, "events",
                    group.Key.Level, group.Key.Status, "triggered_count", group.Count(),
                    "Grouped by trigger date; status is the stored status at generation, not historical status.");
        }

        csv.Row("metadata", start, null, null, null, null, "records", null, null, "total_source_records",
            sourceRows, sourceRows == 0 ? "No matching source records; no measurements were fabricated." : null);
        return csv.ToBytes();
    }

    private static void CheckCount(int count)
    {
        if (count > MaxSourceRows)
            throw new ValidationException($"源记录超过 {MaxSourceRows} 条；请缩小日期范围。未生成截断报表。");
    }

    private static DateTime Period(DateTime date, string reportType) => reportType switch
    {
        "weekly" => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        "monthly" => new DateTime(date.Year, date.Month, 1),
        _ => date.Date
    };

    private sealed class CsvWriter
    {
        private readonly StringBuilder _text = new();
        private readonly CancellationToken _cancellationToken;
        private int _byteCount = 3;

        public CsvWriter(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

        public void Row(params object?[] values)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var line = string.Join(",", values.Select(Cell)) + "\r\n";
            var length = Encoding.UTF8.GetByteCount(line);
            if (length > MaxBytes - _byteCount)
                throw new ValidationException("CSV 超过 8 MiB；请缩小日期范围。未保存截断报表。");
            _byteCount += length;
            _text.Append(line);
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes("\uFEFF" + _text);

        private static string Cell(object? value)
        {
            var text = value switch
            {
                null => string.Empty,
                DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
            // Quoting alone does not prevent spreadsheet formulas, including after leading whitespace.
            if (value is string)
            {
                var first = text.FirstOrDefault(c => !char.IsWhiteSpace(c) && !char.IsControl(c) && c != '\uFEFF');
                if (first is '=' or '+' or '-' or '@' || text.Any(c => c is '\t' or '\r' or '\n'))
                    text = "'" + text;
            }
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
