using System;
using System.Collections.Generic;

namespace NAITSSMS.Domain.Calendar
{
    /// <summary>
    /// Which days a learning centre is expected to keep a register: Monday to Friday, less Nigerian
    /// public holidays and the programme's mid-term breaks. Islamic holidays follow moon sighting, so
    /// those dates are the expected ones and should be corrected when the Federal Government declares.
    /// </summary>
    public static class SchoolCalendar
    {
        private static readonly HashSet<DateTime> PublicHolidays = new()
        {
            // 2025
            new(2025, 1, 1), new(2025, 3, 31), new(2025, 4, 1), new(2025, 4, 18), new(2025, 4, 21),
            new(2025, 5, 1), new(2025, 6, 6), new(2025, 6, 9), new(2025, 6, 12), new(2025, 9, 5),
            new(2025, 10, 1), new(2025, 12, 25), new(2025, 12, 26),
            // 2026
            new(2026, 1, 1), new(2026, 3, 20), new(2026, 3, 23), new(2026, 4, 3), new(2026, 4, 6),
            new(2026, 5, 1), new(2026, 5, 27), new(2026, 5, 28), new(2026, 6, 12), new(2026, 8, 26),
            new(2026, 10, 1), new(2026, 12, 25), new(2026, 12, 28),
            // 2027
            new(2027, 1, 1), new(2027, 3, 10), new(2027, 3, 11), new(2027, 3, 26), new(2027, 3, 29),
            new(2027, 5, 17), new(2027, 6, 14),
        };

        private static readonly (DateTime From, DateTime To)[] MidTermBreaks =
        [
            (new(2025, 10, 27), new(2025, 10, 31)),
            (new(2026, 2, 23), new(2026, 2, 27)),
            (new(2026, 6, 8), new(2026, 6, 12)),
            (new(2026, 11, 2), new(2026, 11, 6)),
        ];

        public static bool IsPublicHoliday(DateTime date) => PublicHolidays.Contains(date.Date);

        public static bool IsMidTermBreak(DateTime date)
        {
            var day = date.Date;
            foreach (var (from, to) in MidTermBreaks)
            {
                if (day >= from && day <= to)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSchoolDay(DateTime date)
            => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
                && !IsPublicHoliday(date)
                && !IsMidTermBreak(date);

        /// <summary>The given day if it is a school day, otherwise the most recent school day before it.</summary>
        public static DateTime LatestSchoolDayOnOrBefore(DateTime date)
        {
            var day = date.Date;
            for (var guard = 0; guard < 30 && !IsSchoolDay(day); guard++)
            {
                day = day.AddDays(-1);
            }

            return day;
        }

        /// <summary>School days from <paramref name="from"/> to <paramref name="to"/>, inclusive.</summary>
        public static List<DateTime> SchoolDaysBetween(DateTime from, DateTime to)
        {
            var days = new List<DateTime>();
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                if (IsSchoolDay(day))
                {
                    days.Add(day);
                }
            }

            return days;
        }
    }
}
