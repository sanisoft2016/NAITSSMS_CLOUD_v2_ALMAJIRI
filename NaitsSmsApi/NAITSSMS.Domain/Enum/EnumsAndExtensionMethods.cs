using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NAITSSMS.Domain.Enum;
namespace NAITSSMS.Domain.Enum
{
   
    public static class EnumsAndExtensionMethods
    {

        public static int GetWeekOfMonth(DateTime date) //Note: The first day of the week is Monday
        {
            var noOfDaysInFirstWeek = 0;
            var beginningOfMonthsDay = new DateTime(date.Year, date.Month, 1).DayOfWeek;
            if (beginningOfMonthsDay == DayOfWeek.Monday)
            {
                noOfDaysInFirstWeek = 7;
            }
            else if (beginningOfMonthsDay == DayOfWeek.Tuesday)
            {
                noOfDaysInFirstWeek = 6;
            }
            else if (beginningOfMonthsDay == DayOfWeek.Wednesday)
            {
                noOfDaysInFirstWeek = 5;
            }
            else if (beginningOfMonthsDay == DayOfWeek.Thursday)
            {
                noOfDaysInFirstWeek = 4;
            }
            else if (beginningOfMonthsDay == DayOfWeek.Friday)
            {
                noOfDaysInFirstWeek = 3;
            }else if (beginningOfMonthsDay == DayOfWeek.Saturday)
            {
                noOfDaysInFirstWeek = 2;
            }
            else if (beginningOfMonthsDay == DayOfWeek.Sunday)
            {
                noOfDaysInFirstWeek = 1;
            }

            var noOfWeekInTheNoOfDaysInTheSelectedDate = 0;
            var noOfDaysInThisMonth = date.Day;

            if (noOfDaysInFirstWeek<=7 && noOfDaysInThisMonth <= 7)
            {
                noOfWeekInTheNoOfDaysInTheSelectedDate = 1;
            }
            else
            {
                var secondWeek = noOfDaysInThisMonth - noOfDaysInFirstWeek;
                int quotient = secondWeek / 7;
                int remainder = secondWeek % 7;

                if (remainder == 0)
                {
                    noOfWeekInTheNoOfDaysInTheSelectedDate = quotient + 1;//Note the constant '1' refer to the first week
                }
                else
                {
                    noOfWeekInTheNoOfDaysInTheSelectedDate = quotient + 2;//Note the constant '2' refer to the first week (1) and the last week (1) in the selected date (1 + 1 = 2)
                }
            }
            return noOfWeekInTheNoOfDaysInTheSelectedDate;
        }

        //public static int GetWeekOfMonth(DateTime date)//This returns business week of the Month
        //{
        //    var today = date;
        //    DateTime beginningOfMonth = new DateTime(date.Year, date.Month, 1);

        //    while (date.Date.AddDays(1).DayOfWeek != CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek)
        //        date = date.AddDays(1);

        //    var virualResult = (int)Math.Truncate((double)date.Subtract(beginningOfMonth).TotalDays / 7f) + 1;
        //    //var today = DateTime.Today;

        //    var noOfDaysInThisMonth = today.Day;

        //    var nameOfDayOfFirstDayOfMonth = DateTime.Today.AddDays(-noOfDaysInThisMonth + 1).DayOfWeek;
        //    if (!(nameOfDayOfFirstDayOfMonth==DayOfWeek.Saturday|| nameOfDayOfFirstDayOfMonth==DayOfWeek.Sunday))//This handle special scenerio
        //    {
        //        return virualResult;
        //    }
        //    var returnResult = virualResult - 1 == 0 ? 1 : virualResult - 1;
        //    return returnResult;
        //}

        public static string ShortenMonth(string month)
        {
            return "";
        }
       public static string GetMonth(byte month)
        {
            switch (month)
            {
                case 1:
                    return "Jan";
                case 2:
                    return "Feb";
                case 3:
                    return "Mar";
                case 4:
                    return "Apr";
                case 5:
                    return "May";
                case 6:
                    return "Jun";
                case 7:
                    return "Jul";
                case 8:
                    return "Aug";
                case 9:
                    return "Sep";
                case 10:
                    return "Oct";
                case 11:
                    return "Nov";
                case 12:
                    return "Dec";
                default:
                    return string.Empty;
            }
        }

        public static IEnumerable<T> GetEnumToList<T>()
        {
            return System.Enum.GetValues(typeof(T)).Cast<T>();
        }
        public static string GetName(this EmployeeQualificationStatus qualificationStatus)
        {
            switch (qualificationStatus)
            {
                case EmployeeQualificationStatus.DegreeInEducation:
                    return "Degree in education";
                case EmployeeQualificationStatus.DegreeInSingleHonour:
                    return "Degree in single-honour";
                case EmployeeQualificationStatus.PgdEducation:
                    return "PGD education";
                case EmployeeQualificationStatus.Nce:
                    return "NCE";
                case EmployeeQualificationStatus.Diploma:
                    return "Diploma";
                case EmployeeQualificationStatus.MasterInEducation:
                    return "Master in education";
                case EmployeeQualificationStatus.MasterInSingleHonour:
                    return "Master in single-honour";
                case EmployeeQualificationStatus.Others:
                    return "Others";
                default:
                    return string.Empty;
            }
        }

        public static string GetName(this CurrentClass currentClass)
        {
            switch (currentClass)
            {
                case CurrentClass.PreNursery:
                    return "Pre Nursery";
                case CurrentClass.Nursery1:
                    return "Nursery 1";
                case CurrentClass.Nursery2:
                    return "Nursery2";

                case CurrentClass.Primary1:
                    return "Primary 1";
                case CurrentClass.Primary2:
                    return "Primary 2";

                case CurrentClass.Primary3:
                    return "Primary 3";
                case CurrentClass.Primary4:
                    return "Primary 4";

                case CurrentClass.Primary5:
                    return "Primary 5";
                case CurrentClass.Primary6:
                    return "Primary 6";

                case CurrentClass.Jss1:
                    return "JSS 1";
                case CurrentClass.Jss2:
                    return "JSS 2";
                case CurrentClass.Jss3:
                    return "JSS 3";

                case CurrentClass.Ss1:
                    return "SS 1";
                case CurrentClass.Ss2:
                    return "SS 2";
                case CurrentClass.Ss3:
                    return "SS 3";
                default:
                    return string.Empty;
            }
        }

        public static string GetName(this AssessmentType assessmentType)
        {
            switch (assessmentType)
            {
                case AssessmentType.FirstCA:
                    return "First C.A.";
                case AssessmentType.SecondCA:
                    return "Second C.A.";
                case AssessmentType.ThirdCA:
                    return "Third C.A.";
                default:
                    return string.Empty;
            }
        }

        public static FeatureImplementationType GetFeatureImplementationTypeInEnum(byte enumValue)
        {
            FeatureImplementationType featureImplementationType = (FeatureImplementationType)enumValue;
            return featureImplementationType;
        }

        public static IList<T> ShuffleMe<T>(this IList<T> list)
        {
            Random random = new Random();
            int n = list.Count;

            for (int i = list.Count - 1; i > 1; i--)
            {
                int rnd = random.Next(i + 1);

                T value = list[rnd];
                list[rnd] = list[i];
                list[i] = value;
            }
            return list;
        }
    }
}