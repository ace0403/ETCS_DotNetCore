using ETCS.Shared.Enumeration;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ETCS.Shared.Helpers
{
    public static class CommonFunctions
    {
        public static DayOfWeek GetDayOfWeek(int dayId)
        {
            if (dayId == (int)DaysEnum.Monday) { return DayOfWeek.Monday; }
            else if (dayId == (int)DaysEnum.Tuesday) { return DayOfWeek.Tuesday; }
            else if (dayId == (int)DaysEnum.Wednesday) { return DayOfWeek.Wednesday; }
            else if (dayId == (int)DaysEnum.Thursday) { return DayOfWeek.Thursday; }
            else if (dayId == (int)DaysEnum.Friday) { return DayOfWeek.Friday; }
            else if (dayId == (int)DaysEnum.Saturday) { return DayOfWeek.Saturday; }
            else return 0;
        }

        public static int GetDayId(DayOfWeek dayOfWeek)
        {
            if (dayOfWeek == DayOfWeek.Monday) { return (int)DaysEnum.Monday; }
            else if (dayOfWeek == DayOfWeek.Tuesday) { return (int)DaysEnum.Tuesday; }
            else if (dayOfWeek == DayOfWeek.Wednesday) { return (int)DaysEnum.Wednesday; }
            else if (dayOfWeek == DayOfWeek.Thursday) { return (int)DaysEnum.Thursday; }
            else if (dayOfWeek == DayOfWeek.Friday) { return (int)DaysEnum.Friday; }
            else if (dayOfWeek == DayOfWeek.Saturday) { return (int)DaysEnum.Saturday; }
            else return 0;
        }

        public static int GetWeekNumberOfMonth(DateTime date)
        {
            date = date.Date;
            DateTime firstMonthDay = new DateTime(date.Year, date.Month, 1);
            DateTime firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            if (firstMonthMonday > date)
            {
                firstMonthDay = firstMonthDay.AddMonths(-1);
                firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            }
            return (date - firstMonthMonday).Days / 7 + 1;
        }

        public static T Trim<T>(this T model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                string? currentValue = property.GetValue(model) as string;

                if (!string.IsNullOrEmpty(currentValue))
                {
                    property.SetValue(model, currentValue.Trim());
                }
            }

            return model;
        }

        public static string TrimString(this string? value)
        {
            if (value == null) return string.Empty;

            value = value.Trim();

            return value;
        }

        public static List<string> ParseCommaSeparatedString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .ToList();
        }

        public static List<int> ParseCommaSeparatedIntString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<int>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => int.TryParse(s, out _))
                       .Select(int.Parse)
                       .ToList();
        }

        public static List<T> ParseListJson<T>(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return new List<T>();

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<List<T>>(jsonString, options)
                       ?? new List<T>();
            }
            catch (JsonException)
            {
                return new List<T>();
            }
        }
    }
}
