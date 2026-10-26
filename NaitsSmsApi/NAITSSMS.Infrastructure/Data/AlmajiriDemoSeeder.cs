using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NAITSSMS.Domain.Calendar;
using NAITSSMS.Domain.Entities;
using NAITSSMS.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Data
{
    /// <summary>
    /// Builds the Commission programme demonstration: learning centres across three geopolitical
    /// zones, a complete archived 2025/2026 session (three terms of scores, registers, report-card
    /// comments and the year's transitions) and the current 2026/2027 First Term, with daily
    /// registers up to today.
    ///
    /// Deterministic: every random choice is hashed from a stable key (username, date, subject), so
    /// a re-run produces identical data. Idempotent: a centre with a <see cref="ProgrammeCentreProfile"/>
    /// is complete and is only topped up with the school days registered since the last run.
    /// </summary>
    public static class AlmajiriDemoSeeder
    {
        public const string DemoPassword = "Naits@Demo2026";

        /// <summary>The Commission's own login; also the onboarding agent for every centre.</summary>
        public const string CommissionUserName = "commission.officer";

        public const string CommissionRole = "Commission";

        private const string ArchiveSession = "2025/2026";
        private const string CurrentSession = "2026/2027";

        private static readonly (TermStatus Term, DateTime Start, DateTime End)[] ArchiveTerms =
        [
            (TermStatus.First, new(2025, 9, 15), new(2025, 12, 12)),
            (TermStatus.Second, new(2026, 1, 12), new(2026, 4, 2)),
            (TermStatus.Third, new(2026, 4, 27), new(2026, 7, 24)),
        ];

        private static readonly DateTime CurrentTermStart = new(2026, 9, 14);
        private static readonly DateTime CurrentTermEnd = new(2026, 12, 11);
        private static readonly DateTime NextTermBegins = new(2027, 1, 11);

        /// <summary>The drop-out cohort stops attending from here, so the streak grows each day.</summary>
        private static readonly DateTime DropOutStreakFrom = new(2026, 10, 12);

        /// <summary>The weakest centre's register stalls after this date: the overdue case.</summary>
        private static readonly DateTime StalledRegisterAfter = new(2026, 10, 16);

        /// <summary>
        /// One centre keeps its class registers but stops the staff register after this date: the
        /// case where learners are recorded but nobody is accounting for the facilitators.
        /// </summary>
        private static readonly DateTime StalledStaffRegisterAfter = new(2026, 10, 19);

        /// <summary>Last day at the centre for learners who withdrew during the archived session.</summary>
        private static readonly DateTime MidYearWithdrawalDate = new(2026, 2, 13);

        private enum CentreKind
        {
            Tsangaya,
            GirlsCentre,
            Community
        }

        private sealed record CentreBlueprint(
            string Slug,
            string Code,
            string StateCode,
            string Name,
            string Community,
            byte StateId,
            string StateName,
            short LgaId,
            string LgaName,
            CentreKind Kind,
            string CentreType,
            int LearnerCount,
            int FacilitatorCount,
            double GirlShare,
            double LearnerAttendanceBase,
            double FacilitatorAttendanceBase,
            string HeadName,
            short YearEstablished,
            (byte StateId, double Weight)[] OriginStates,
            string[] Tsangayas);

        private const string TsangayaType = "Integrated Tsangaya (Almajiri) centre";
        private const string GirlsType = "Out-of-school girls' second-chance centre";
        private const string CommunityType = "Community learning centre (out-of-school children)";

        private static readonly CentreBlueprint[] Centres =
        [
            new("kano-gwale", "GWL", "KN", "Tsangaya Almajiri Integrated Centre, Gwale", "Gwale", 20, "Kano", 421, "Gwale",
                CentreKind.Tsangaya, TsangayaType, 62, 6, 0, 0.94, 0.97, "Malam Bashir Abdullahi Gwale", 2019,
                [(20, 0.55), (18, 0.25), (21, 0.12), (5, 0.08)],
                ["Tsangayar Malam Nasiru", "Tsangayar Malam Haruna", "Tsangayar Gwani Ali"]),
            new("kano-dala", "DAL", "KN", "Almajiri Integrated Learning Centre, Dala", "Dala", 20, "Kano", 410, "Dala",
                CentreKind.Tsangaya, TsangayaType, 56, 5, 0, 0.88, 0.92, "Malam Sani Yakubu Dala", 2020,
                [(20, 0.6), (18, 0.25), (21, 0.15)],
                ["Tsangayar Malam Sule", "Tsangayar Malam Habibu", "Tsangayar Gwani Sabo"]),
            new("kaduna-giwa", "GWA", "KD", "Tsangaya Model Centre, Giwa", "Giwa", 19, "Kaduna", 399, "Giwa",
                CentreKind.Tsangaya, TsangayaType, 52, 5, 0, 0.86, 0.71, "Malam Lawal Isah Giwa", 2018,
                [(19, 0.45), (20, 0.2), (37, 0.15), (21, 0.12), (27, 0.08)],
                ["Tsangayar Malam Auwalu", "Tsangayar Malam Kabiru"]),
            new("kaduna-rigasa", "RGS", "KD", "Almajiri Skills Centre, Rigasa", "Rigasa", 19, "Kaduna", 401, "Igabi",
                CentreKind.Tsangaya, TsangayaType, 48, 4, 0.08, 0.79, 0.88, "Malam Umar Dauda Rigasa", 2021,
                [(19, 0.5), (20, 0.2), (37, 0.15), (21, 0.15)],
                ["Tsangayar Malam Mustapha", "Tsangayar Malam Garba"]),
            new("sokoto-wamakko", "WMK", "SO", "Almajiri Community Learning Centre, Wamakko", "Wamakko", 34, "Sokoto", 780, "Wamakko",
                CentreKind.Tsangaya, TsangayaType, 42, 4, 0, 0.71, 0.83, "Malam Bello Shehu Wamakko", 2021,
                [(34, 0.6), (22, 0.25), (37, 0.15)],
                ["Tsangayar Malam Abubakar", "Tsangayar Malam Shehu"]),
            new("sokoto-bodinga", "BDG", "SO", "Tsangaya Almajiri Centre, Bodinga", "Bodinga", 34, "Sokoto", 762, "Bodinga",
                CentreKind.Tsangaya, TsangayaType, 44, 4, 0, 0.91, 0.9, "Malam Aliyu Muhammad Bodinga", 2020,
                [(34, 0.7), (22, 0.2), (37, 0.1)],
                ["Tsangayar Malam Bala", "Tsangayar Malam Tukur"]),
            new("katsina-daura", "DRA", "KT", "Girls' Second-Chance Learning Centre, Daura", "Daura", 21, "Katsina", 456, "Daura",
                CentreKind.GirlsCentre, GirlsType, 50, 5, 1, 0.9, 0.94, "Malama Hadiza Yusuf Daura", 2022,
                [(21, 1.0)],
                []),
            new("zamfara-gusau", "GSU", "ZM", "Tsangaya Integrated Centre, Gusau", "Gusau", 37, "Zamfara", 823, "Gusau",
                CentreKind.Tsangaya, TsangayaType, 46, 4, 0, 0.84, 0.86, "Malam Murtala Salihu Gusau", 2020,
                [(37, 0.7), (34, 0.15), (21, 0.15)],
                ["Tsangayar Malam Jibrin", "Tsangayar Malam Danladi"]),
            new("jigawa-dutse", "DUT", "JG", "Community Learning Centre, Dutse", "Dutse", 18, "Jigawa", 376, "Dutse",
                CentreKind.Community, CommunityType, 48, 4, 0.48, 0.87, 0.9, "Malama Rabi Sani Dutse", 2022,
                [(18, 0.8), (20, 0.2)],
                []),
            new("bauchi-bauchi", "BCH", "BA", "Almajiri Integrated Model School, Bauchi", "Bauchi", 5, "Bauchi", 106, "Bauchi",
                CentreKind.Tsangaya, TsangayaType, 54, 5, 0.06, 0.89, 0.9, "Malam Yahaya Garba Bauchi", 2015,
                [(5, 0.6), (16, 0.2), (18, 0.2)],
                ["Tsangayar Malam Idris", "Tsangayar Malam Adamu", "Tsangayar Gwani Musa"]),
            new("borno-maiduguri", "MAI", "BO", "Gwange Community Learning Centre, Maiduguri", "Gwange", 8, "Borno", 170, "Maiduguri",
                CentreKind.Community, CommunityType, 58, 5, 0.52, 0.82, 0.87, "Malam Bukar Modu Gwange", 2019,
                [(8, 0.9), (36, 0.1)],
                []),
            new("yobe-damaturu", "DMT", "YB", "Tsangaya Integrated Centre, Damaturu", "Damaturu", 36, "Yobe", 802, "Damaturu",
                CentreKind.Tsangaya, TsangayaType, 40, 4, 0, 0.87, 0.9, "Malam Kolo Ibrahim Damaturu", 2016,
                [(36, 0.7), (8, 0.2), (18, 0.1)],
                ["Tsangayar Malam Bukar", "Tsangayar Malam Goni"]),
            new("niger-minna", "MNA", "NG", "Tsangaya Integrated Centre, Minna", "Minna", 27, "Niger", 581, "Chanchaga",
                CentreKind.Tsangaya, TsangayaType, 44, 4, 0, 0.88, 0.91, "Malam Nuhu Abubakar Minna", 2017,
                [(27, 0.6), (22, 0.2), (19, 0.2)],
                ["Tsangayar Malam Zakari", "Tsangayar Malam Bawa"]),
            new("fct-kubwa", "KBW", "FC", "Kubwa Out-of-School Children Learning Centre, Abuja", "Kubwa", 15, "FCT", 322, "Abuja",
                CentreKind.Community, CommunityType, 46, 5, 0.5, 0.92, 0.95, "Malama Zainab Musa Kubwa", 2023,
                [(15, 0.3), (27, 0.2), (19, 0.2), (20, 0.15), (5, 0.15)],
                []),
        ];

        private static readonly string[] SubjectNames =
        [
            "Qur'anic Recitation (Tilawah)",
            "Islamic Studies",
            "Hadith and Fiqh",
            "Arabic Language",
            "Hausa Literacy",
            "English Literacy",
            "Numeracy",
            "Basic Science and Hygiene",
            "Civic and Social Studies",
            "Vocational Skills",
        ];

        /// <summary>Typical subject averages (%) by centre kind, before learner ability and term growth.</summary>
        private static readonly Dictionary<CentreKind, double[]> SubjectBaseline = new()
        {
            [CentreKind.Tsangaya] = [62, 58, 54, 51, 52, 40, 42, 48, 50, 56],
            [CentreKind.GirlsCentre] = [51, 54, 48, 41, 57, 48, 46, 52, 54, 61],
            [CentreKind.Community] = [52, 52, 48, 42, 54, 45, 45, 50, 52, 58],
        };

        /// <summary>Subjects taught by each facilitator, by centre size (index into <see cref="SubjectNames"/>).</summary>
        private static readonly Dictionary<int, int[][]> FacilitatorSubjects = new()
        {
            [4] = [[0, 1, 2, 3], [4, 5, 8], [6, 7], [9]],
            [5] = [[0, 1, 2], [3, 4], [5, 8], [6, 7], [9]],
            [6] = [[0, 1, 2], [3, 4], [5, 8], [6, 7], [9], [5, 6]],
        };

        private static readonly string[] FacilitatorDisciplines =
        [
            "Qur'anic and Islamic Studies",
            "Arabic and Hausa",
            "English and Civic Education",
            "Mathematics and Basic Science",
            "Vocational Skills",
            "Literacy and Numeracy",
        ];

        private static readonly (byte Id, string Name)[] AffectiveItems =
        [
            (1, "Attentiveness"),
            (2, "Honesty"),
            (3, "Neatness"),
            (4, "Politeness"),
            (5, "Punctuality"),
            (6, "Relationship with Others"),
            (7, "Club/Society"),
            (8, "Drawing and Painting"),
            (9, "Hand Writing"),
            (10, "Hobbies"),
            (11, "Speech Fluency"),
            (12, "Sport and Game"),
        ];

        /// <summary>Programme centres print these instead of the formal-school wording.</summary>
        private static readonly (byte Id, string Label)[] ProgrammeAffectiveLabels =
        [
            (1, "Attentiveness"),
            (2, "Honesty"),
            (3, "Personal Hygiene"),
            (4, "Respect and Politeness"),
            (5, "Punctuality"),
            (6, "Relationship with Others"),
            (7, "Qur'an Memorisation"),
            (8, "Ajami Writing"),
            (9, "Handwriting (Boko)"),
            (10, "Vocational Practical"),
            (11, "Recitation Fluency"),
            (12, "Games and Teamwork"),
        ];

        private static readonly string[] MaleFirstNames =
        [
            "Abubakar", "Muhammad", "Ibrahim", "Usman", "Aliyu", "Sani", "Bashir", "Yusuf", "Nasir",
            "Sulaiman", "Kabiru", "Auwal", "Shehu", "Lawal", "Umar", "Salisu", "Mustapha", "Haruna",
            "Bilal", "Idris", "Aminu", "Danladi", "Garba", "Mahmud", "Tijjani", "Zubairu", "Rabiu",
            "Murtala", "Adamu", "Jibrin", "Isma'il", "Hamza", "Anas", "Abdullahi", "Nura", "Faruk",
            "Sadiq", "Ahmad", "Yakubu", "Musa", "Hassan", "Husaini", "Aminu", "Habibu", "Zakari",
            "Sabiu", "Muttaka", "Abdulrahman", "Abdulkadir", "Bello",
        ];

        private static readonly string[] FemaleFirstNames =
        [
            "Aisha", "Fatima", "Zainab", "Hauwa", "Maryam", "Hadiza", "Khadija", "Amina", "Rukayya",
            "Halima", "Safiya", "Balkisu", "Zulaihat", "Ramlat", "Asmau", "Bilkisu", "Firdausi",
            "Jamila", "Laraba", "Sadiya", "Hafsat", "Nafisa", "Ummi", "Maimuna", "Sa'adatu", "Rabi",
            "Habiba", "Zahra'u", "Hussaina", "Murja",
        ];

        private static readonly string[] KanuriMaleFirstNames =
        [
            "Modu", "Bukar", "Kolo", "Ali", "Goni", "Mustapha", "Babagana", "Lawan", "Kachalla",
            "Zanna", "Mohammed", "Abba", "Umar", "Ibrahim", "Bulama", "Gana", "Mala", "Kyari",
        ];

        private static readonly string[] KanuriFemaleFirstNames =
        [
            "Fanta", "Yagana", "Falmata", "Aisha", "Kaltum", "Hauwa", "Zara", "Amina", "Fatima", "Bintu",
            "Ya Kaka", "Inna",
        ];

        private static readonly string[] SurNames =
        [
            "Abdullahi", "Muhammad", "Sani", "Bello", "Yakubu", "Aliyu", "Suleiman", "Hassan", "Umar",
            "Ibrahim", "Musa", "Lawal", "Shehu", "Danjuma", "Gambo", "Maikudi", "Tanko", "Dauda",
            "Isah", "Nuhu", "Kabir", "Salihu", "Zango", "Mainasara", "Wada", "Rufai", "Tukur",
            "Garba", "Haruna", "Adamu", "Idris", "Yusuf", "Sule", "Bala", "Abubakar", "Usman",
            "Inuwa", "Dahiru", "Gwadabe", "Kurawa", "Dankano", "Shuaibu",
        ];

        private static readonly string[] KanuriSurNames =
        [
            "Modu", "Bukar", "Kolo", "Kachalla", "Goni", "Zanna", "Lawan", "Kyari", "Mala", "Bulama",
            "Gana", "Abba", "Babagana", "Shettima",
        ];

        private static readonly string[] GuardianOccupations =
        [
            "Farmer", "Farmer", "Farmer", "Trader", "Petty trader", "Herder", "Tailor", "Butcher",
            "Driver", "Artisan", "Fisherman", "Labourer", "Shoemaker", "Blacksmith",
        ];

        private static readonly string[] MotherOccupations =
        [
            "Petty trader", "Food vendor", "Farmer", "Tailor", "Housewife", "Housewife",
        ];

        private static readonly string[] PhonePrefixes =
        [
            "0803", "0806", "0813", "0816", "0703", "0706", "0810", "0814", "0903", "0906", "0805",
            "0807", "0815", "0905", "0802", "0808", "0812", "0701", "0902", "0901",
        ];

        private static readonly string[] BoysTrades =
        [
            "Tailoring", "Carpentry", "Welding", "Phone repair", "Shoe making", "Auto mechanic",
        ];

        private static readonly string[] GirlsTrades =
        [
            "Tailoring", "Knitting", "Soap making", "Catering", "Cosmetology",
        ];

        public sealed class SeedReport
        {
            public bool AlreadySeeded { get; set; }
            public int Centres { get; set; }
            public int Learners { get; set; }
            public int Facilitators { get; set; }
            public int Subjects { get; set; }
            public int AssessmentRows { get; set; }
            public int LearnerAttendanceRows { get; set; }
            public int FacilitatorAttendanceRows { get; set; }
            public List<string> Logins { get; set; } = new();
            public string Message { get; set; } = string.Empty;
        }

        public static async Task<SeedReport> SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;

            var db = sp.GetRequiredService<AppDbContext>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            var report = new SeedReport();

            await SchemaRepair.ApplyAsync(db);
            var roleIds = await EnsureRolesAsync(db, ct);
            await EnsureAffectiveItemsAsync(db, ct);
            await EnsureSchoolHeadCategoriesAsync(db, ct);

            // One hash reused across every seeded login: hashing ~800 times is the slow part.
            var passwordHash = userManager.PasswordHasher.HashPassword(
                new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = "x", MiddleName = "x", SurName = "x" },
                DemoPassword);

            var commissionUserId = await EnsureCommissionOfficerAsync(db, roleIds, passwordHash, ct);
            report.Logins.Add($"Commission oversight → {CommissionUserName} / {DemoPassword}");

            var lgasByState = (await db.LocalGovernments.AsNoTracking().ToListAsync(ct))
                .GroupBy(lga => lga.StateId)
                .ToDictionary(group => group.Key, group => group.OrderBy(lga => lga.Id).ToList());

            var stateNames = await db.States.AsNoTracking().ToDictionaryAsync(state => state.Id, state => state.Name, ct);

            db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                foreach (var blueprint in Centres)
                {
                    var world = new CentreWorld(blueprint, lgasByState, stateNames);
                    var schoolId = await EnsureCentreAsync(db, world, commissionUserId, roleIds, passwordHash, report, ct);
                    await TopUpCurrentTermAsync(db, world, schoolId, report, ct);
                    report.Logins.Add($"{blueprint.Name} → {blueprint.Slug}.admin / {DemoPassword}");
                }
            }
            finally
            {
                db.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            report.Logins.Add($"Facilitators → <centre-slug>.fac01..facNN / {DemoPassword}");
            report.Logins.Add($"Learners → <centre-slug>.lnr001..lnrNNN / {DemoPassword}");

            report.Centres = Centres.Length;
            report.Subjects = SubjectNames.Length;
            report.AlreadySeeded = report.Learners == 0;
            report.Message = report.AlreadySeeded
                ? $"Demo centres already present; registers topped up with {report.LearnerAttendanceRows} learner "
                  + $"and {report.FacilitatorAttendanceRows} facilitator mark(s)."
                : $"Seeded {report.Centres} centres, {report.Learners} learners, {report.Facilitators} facilitators, "
                  + $"{report.AssessmentRows} score rows and {report.LearnerAttendanceRows} daily learner marks.";

            return report;
        }

        // ------------------------------------------------------------------------------------
        // Deterministic randomness
        // ------------------------------------------------------------------------------------

        /// <summary>FNV-1a: stable across processes, unlike <see cref="string.GetHashCode()"/>.</summary>
        private static ulong StableHash(string key)
        {
            const ulong offset = 14695981039346656037;
            const ulong prime = 1099511628211;
            var hash = offset;
            foreach (var ch in key)
            {
                hash ^= ch;
                hash *= prime;
            }

            // Final avalanche so nearby keys do not produce nearby values.
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccd;
            hash ^= hash >> 33;
            return hash;
        }

        /// <summary>Uniform in [0, 1).</summary>
        private static double Unit(string key) => (StableHash(key) >> 11) * (1.0 / (1UL << 53));

        /// <summary>Standard normal, by Box–Muller over two hashed uniforms.</summary>
        private static double Normal(string key)
        {
            var u1 = Math.Max(Unit(key + "#a"), 1e-9);
            var u2 = Unit(key + "#b");
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private static T Pick<T>(IReadOnlyList<T> items, string key) => items[(int)(StableHash(key) % (ulong)items.Count)];

        private static string Phone(string key)
        {
            var prefix = Pick(PhonePrefixes, key + "#prefix");
            var digits = (StableHash(key + "#digits") % 10_000_000UL).ToString("0000000");
            return prefix + digits;
        }

        // ------------------------------------------------------------------------------------
        // Per-centre plan
        // ------------------------------------------------------------------------------------

        private enum LearnerFate
        {
            Active,
            Graduated,
            WithdrawnMidYear
        }

        private sealed class LearnerPlan
        {
            public required string UserName { get; init; }
            public required int Sequence { get; init; }
            public Gender Gender { get; init; }
            public required string FirstName { get; init; }
            public required string MiddleName { get; init; }
            public required string SurName { get; init; }

            /// <summary>1–3 for learners on roll now; the last level attended for those who left.</summary>
            public int Level { get; init; }

            /// <summary>Level in the archived session, or 0 for this year's new intake.</summary>
            public int ArchiveLevel { get; init; }

            public LearnerFate Fate { get; init; }
            public DateTime Admission { get; init; }
            public DateTime DateOfBirth { get; init; }
            public LearnerProgrammeCategory Category { get; init; }
            public byte OriginStateId { get; init; }
            public short? OriginLgaId { get; init; }
            public string OriginLgaName { get; init; } = string.Empty;
            public string Tsangaya { get; init; } = string.Empty;
            public double Ability { get; init; }
            public bool DropOutRisk { get; init; }
            public double AttendancePropensity { get; init; }
        }

        private sealed class FacilitatorPlan
        {
            public required string UserName { get; init; }
            public required int Index { get; init; }
            public Gender Gender { get; init; }
            public required string FirstName { get; init; }
            public required string MiddleName { get; init; }
            public required string SurName { get; init; }
            public required string Discipline { get; init; }
            public EmployeeQualificationStatus Qualification { get; init; }
            public DateTime DateOfBirth { get; init; }
            public double AttendancePropensity { get; init; }
            public required int[] Subjects { get; init; }
        }

        private sealed class CentreWorld
        {
            public CentreBlueprint Blueprint { get; }
            public List<LearnerPlan> Learners { get; } = new();
            public List<FacilitatorPlan> Facilitators { get; } = new();
            private readonly IReadOnlyDictionary<byte, List<LocalGovernment>> _lgasByState;
            private readonly IReadOnlyDictionary<byte, string> _stateNames;

            public CentreWorld(
                CentreBlueprint blueprint,
                IReadOnlyDictionary<byte, List<LocalGovernment>> lgasByState,
                IReadOnlyDictionary<byte, string> stateNames)
            {
                Blueprint = blueprint;
                _lgasByState = lgasByState;
                _stateNames = stateNames;
                PlanFacilitators();
                PlanLearners();
            }

            /// <summary>Reference data has some lower-case names ("katsina"); print them capitalised.</summary>
            public string StateName(byte stateId)
                => _stateNames.TryGetValue(stateId, out var name) && name.Length > 0
                    ? char.ToUpperInvariant(name[0]) + name[1..]
                    : string.Empty;

            private bool IsKanuriArea => Blueprint.StateId is 8 or 36;

            private void PlanFacilitators()
            {
                var b = Blueprint;
                var groups = FacilitatorSubjects[Math.Clamp(b.FacilitatorCount, 4, 6)];

                for (var index = 0; index < b.FacilitatorCount; index++)
                {
                    var key = $"{b.Slug}.fac{index + 1:00}";

                    // Girls' centres are staffed by women; elsewhere the literacy/numeracy posts
                    // are where women facilitators are most often found.
                    var female = b.Kind switch
                    {
                        CentreKind.GirlsCentre => index != 0,
                        CentreKind.Community => index % 2 == 1,
                        _ => b.FacilitatorCount >= 5 && index == 3,
                    };

                    var firstPool = female
                        ? (IsKanuriArea ? KanuriFemaleFirstNames : FemaleFirstNames)
                        : (IsKanuriArea ? KanuriMaleFirstNames : MaleFirstNames);
                    var surPool = IsKanuriArea ? KanuriSurNames : SurNames;

                    var qualification = index == 0 && b.Kind == CentreKind.Tsangaya
                        ? EmployeeQualificationStatus.Diploma
                        : Pick(
                            new[]
                            {
                                EmployeeQualificationStatus.Nce, EmployeeQualificationStatus.Nce,
                                EmployeeQualificationStatus.Nce, EmployeeQualificationStatus.DegreeInEducation,
                                EmployeeQualificationStatus.Diploma,
                            },
                            key + "#qual");

                    // The facilitator-cover story: two named facilitators carry most of the absence.
                    var propensity = b.FacilitatorAttendanceBase + (Unit(key + "#att") - 0.5) * 0.08;
                    if (b.Slug == "kaduna-giwa")
                    {
                        propensity = index switch
                        {
                            1 => 0.5,
                            3 => 0.58,
                            _ => 0.85 + (Unit(key + "#att") - 0.5) * 0.06,
                        };
                    }

                    Facilitators.Add(new FacilitatorPlan
                    {
                        UserName = key,
                        Index = index,
                        Gender = female ? Gender.Female : Gender.Male,
                        FirstName = Pick(firstPool, key + "#first"),
                        MiddleName = Pick(IsKanuriArea ? KanuriMaleFirstNames : MaleFirstNames, key + "#middle"),
                        SurName = Pick(surPool, key + "#sur"),
                        Discipline = FacilitatorDisciplines[Math.Min(index, FacilitatorDisciplines.Length - 1)],
                        Qualification = qualification,
                        DateOfBirth = new DateTime(1974, 1, 1).AddDays((int)(Unit(key + "#dob") * 365 * 24)),
                        AttendancePropensity = Math.Clamp(propensity, 0.4, 0.99),
                        Subjects = groups[index],
                    });
                }
            }

            private void PlanLearners()
            {
                var b = Blueprint;
                var onRoll = b.LearnerCount;
                var level1 = (int)Math.Round(onRoll * 0.36);
                var level2 = (int)Math.Round(onRoll * 0.33);
                var level3 = onRoll - level1 - level2;
                var graduates = (int)Math.Round(onRoll * 0.28);
                var withdrawn = Math.Max(1, (int)Math.Round(onRoll * 0.03));

                // Level 3 first so <slug>.lnr001 is a returning learner with a full archived year.
                var slots = new List<(int Level, int ArchiveLevel, LearnerFate Fate)>();
                slots.AddRange(Enumerable.Repeat((3, 2, LearnerFate.Active), level3));
                slots.AddRange(Enumerable.Repeat((2, 1, LearnerFate.Active), level2));
                slots.AddRange(Enumerable.Repeat((1, 0, LearnerFate.Active), level1));
                slots.AddRange(Enumerable.Repeat((3, 3, LearnerFate.Graduated), graduates));
                for (var i = 0; i < withdrawn; i++)
                {
                    var level = i % 2 == 0 ? 1 : 2;
                    slots.Add((level, level, LearnerFate.WithdrawnMidYear));
                }

                for (var index = 0; index < slots.Count; index++)
                {
                    var (level, archiveLevel, fate) = slots[index];
                    var userName = $"{b.Slug}.lnr{index + 1:000}";
                    var key = userName;

                    var isFemale = b.GirlShare >= 1 || Unit(key + "#gender") < b.GirlShare;
                    var gender = isFemale ? Gender.Female : Gender.Male;

                    var firstPool = isFemale
                        ? (IsKanuriArea ? KanuriFemaleFirstNames : FemaleFirstNames)
                        : (IsKanuriArea ? KanuriMaleFirstNames : MaleFirstNames);
                    var malePool = IsKanuriArea ? KanuriMaleFirstNames : MaleFirstNames;
                    var surPool = IsKanuriArea ? KanuriSurNames : SurNames;

                    var category = ResolveCategory(b, isFemale, key);
                    var (originStateId, originLga) = ResolveOrigin(b, category, key);

                    // Almajiri learners are a year or so older than formal-school peers at each level.
                    var baseAge = level switch { 1 => 7, 2 => 8, _ => 9 };
                    if (fate == LearnerFate.Graduated)
                    {
                        baseAge = 10;
                    }

                    if (category == LearnerProgrammeCategory.Almajiri)
                    {
                        baseAge += 1;
                    }

                    var age = baseAge + (int)(Unit(key + "#age") * 4);
                    var dateOfBirth = new DateTime(2026, 10, 1).AddYears(-age).AddDays(-(int)(Unit(key + "#dob") * 360));

                    var admission = (fate, archiveLevel, level) switch
                    {
                        (LearnerFate.Active, 0, _) => Unit(key + "#late") < 0.1
                            ? new DateTime(2026, 9, 28)
                            : CurrentTermStart,
                        (LearnerFate.Graduated, _, _) => new DateTime(2023, 9, 18),
                        (_, 1, _) => ArchiveTerms[0].Start,
                        _ => new DateTime(2024, 9, 16),
                    };

                    var tsangaya = category == LearnerProgrammeCategory.Almajiri && b.Tsangayas.Length > 0
                        ? Pick(b.Tsangayas, key + "#tsangaya")
                        : string.Empty;

                    Learners.Add(new LearnerPlan
                    {
                        UserName = userName,
                        Sequence = index + 1,
                        Gender = gender,
                        FirstName = Pick(firstPool, key + "#first"),
                        MiddleName = Pick(malePool, key + "#father"),
                        SurName = Pick(surPool, key + "#sur"),
                        Level = level,
                        ArchiveLevel = archiveLevel,
                        Fate = fate,
                        Admission = admission,
                        DateOfBirth = dateOfBirth,
                        Category = category,
                        OriginStateId = originStateId,
                        OriginLgaId = originLga?.Id,
                        OriginLgaName = originLga?.Name ?? string.Empty,
                        Tsangaya = tsangaya,
                        Ability = Normal(key + "#ability"),
                        DropOutRisk = fate == LearnerFate.Active && index % 17 == 3,
                        AttendancePropensity = Math.Clamp(
                            b.LearnerAttendanceBase + Normal(key + "#att") * 0.045, 0.45, 0.995),
                    });
                }
            }

            private static LearnerProgrammeCategory ResolveCategory(CentreBlueprint b, bool isFemale, string key)
            {
                var roll = Unit(key + "#category");
                return b.Kind switch
                {
                    CentreKind.Tsangaya when !isFemale => LearnerProgrammeCategory.Almajiri,
                    CentreKind.Tsangaya => LearnerProgrammeCategory.OutOfSchoolNeverEnrolled,
                    CentreKind.GirlsCentre => roll < 0.6
                        ? LearnerProgrammeCategory.OutOfSchoolNeverEnrolled
                        : LearnerProgrammeCategory.OutOfSchoolDroppedOut,
                    _ when b.StateId == 8 => roll < 0.6
                        ? LearnerProgrammeCategory.DisplacedChild
                        : LearnerProgrammeCategory.OutOfSchoolNeverEnrolled,
                    _ => roll < 0.55
                        ? LearnerProgrammeCategory.OutOfSchoolNeverEnrolled
                        : LearnerProgrammeCategory.OutOfSchoolDroppedOut,
                };
            }

            private (byte StateId, LocalGovernment? Lga) ResolveOrigin(
                CentreBlueprint b,
                LearnerProgrammeCategory category,
                string key)
            {
                // Almajiri migrate to their Tsangaya; other out-of-school children are mostly local.
                byte stateId = b.StateId;
                if (category == LearnerProgrammeCategory.Almajiri || b.Slug == "fct-kubwa")
                {
                    var roll = Unit(key + "#origin");
                    var cumulative = 0d;
                    foreach (var (candidate, weight) in b.OriginStates)
                    {
                        cumulative += weight;
                        if (roll < cumulative)
                        {
                            stateId = candidate;
                            break;
                        }
                    }
                }

                if (!_lgasByState.TryGetValue(stateId, out var lgas) || lgas.Count == 0)
                {
                    return (stateId, null);
                }

                if (stateId == b.StateId && Unit(key + "#local") < 0.45)
                {
                    var own = lgas.FirstOrDefault(lga => lga.Id == b.LgaId);
                    if (own != null)
                    {
                        return (stateId, own);
                    }
                }

                return (stateId, Pick(lgas, key + "#lga"));
            }
        }

        // ------------------------------------------------------------------------------------
        // Roles, reference data, Commission login
        // ------------------------------------------------------------------------------------

        private static async Task<Dictionary<string, string>> EnsureRolesAsync(AppDbContext db, CancellationToken ct)
        {
            var wanted = new[] { "SAOfficer", CommissionRole, "Registry", "Teacher", "StudentOrParent" };
            var existing = await db.Roles.ToListAsync(ct);

            foreach (var role in wanted)
            {
                if (existing.Any(row => string.Equals(row.Name, role, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var created = new IdentityRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = role,
                    NormalizedName = role.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                };
                db.Roles.Add(created);
                existing.Add(created);
            }

            await db.SaveChangesAsync(ct);
            return existing
                .Where(row => row.Name != null)
                .GroupBy(row => row.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task EnsureUserRoleAsync(
            AppDbContext db,
            string userId,
            string roleId,
            CancellationToken ct)
        {
            if (!await db.UserRoles.AnyAsync(row => row.UserId == userId && row.RoleId == roleId, ct))
            {
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleId });
            }
        }

        private static async Task EnsureAffectiveItemsAsync(AppDbContext db, CancellationToken ct)
        {
            var existing = await db.AffectiveAndPsychomotors.Select(item => item.Id).ToListAsync(ct);
            var missing = AffectiveItems
                .Where(item => !existing.Contains(item.Id))
                .Select(item => new AffectiveXzAndPsychomotor { Id = item.Id, ItemsName = item.Name })
                .ToList();

            if (missing.Count > 0)
            {
                db.AffectiveAndPsychomotors.AddRange(missing);
                await db.SaveChangesAsync(ct);
            }
        }

        private static async Task EnsureSchoolHeadCategoriesAsync(AppDbContext db, CancellationToken ct)
        {
            // ResolveSchoolHeadCategoryId maps HeadTeacher → 1 and Principal → 2.
            var wanted = new (byte Id, HeadStatus Status)[]
            {
                (1, HeadStatus.HeadTeacher),
                (2, HeadStatus.Principal),
            };

            var existing = await db.SchoolHeadCategories.Select(row => row.Id).ToListAsync(ct);
            var missing = wanted
                .Where(row => !existing.Contains(row.Id))
                .Select(row => new SchoolHeadTxCategory { Id = row.Id, HeadStatus = row.Status })
                .ToList();

            if (missing.Count > 0)
            {
                db.SchoolHeadCategories.AddRange(missing);
                await db.SaveChangesAsync(ct);
            }
        }

        private static async Task<string> EnsureCommissionOfficerAsync(
            AppDbContext db,
            IReadOnlyDictionary<string, string> roleIds,
            string passwordHash,
            CancellationToken ct)
        {
            var officer = await db.Users.FirstOrDefaultAsync(u => u.UserName == CommissionUserName, ct);
            if (officer == null)
            {
                officer = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = CommissionUserName,
                    NormalizedUserName = CommissionUserName.ToUpperInvariant(),
                    Email = "commission@almajiri.demo",
                    NormalizedEmail = "COMMISSION@ALMAJIRI.DEMO",
                    EmailConfirmed = true,
                    FirstName = "Programme",
                    MiddleName = "Monitoring",
                    SurName = "Officer",
                    Gender = Gender.Female,
                    CreatedDate = DateTime.UtcNow,
                    UserStatus = UserStatus.Agent,
                    AccessStatus = AccessStatus.Active,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    PasswordHash = passwordHash,
                };

                db.Users.Add(officer);
                await db.SaveChangesAsync(ct);
            }

            // Commission, not SAOfficer: the Commission sees programme centres only, never the
            // platform's other schools or their licensing.
            await EnsureUserRoleAsync(db, officer.Id, roleIds[CommissionRole], ct);
            var saOfficerLink = await db.UserRoles
                .FirstOrDefaultAsync(row => row.UserId == officer.Id && row.RoleId == roleIds["SAOfficer"], ct);
            if (saOfficerLink != null)
            {
                db.UserRoles.Remove(saOfficerLink);
            }

            if (!await db.Agents.AnyAsync(a => a.UserId == officer.Id, ct))
            {
                db.Agents.Add(new Agent
                {
                    UserId = officer.Id,
                    TimeStatus = TimeStatus.Current,
                    FullAddress = "National Commission for Almajiri and Out-of-School Children Education, Abuja",
                    SchoolRegId = "ALM001",
                });
            }

            await db.SaveChangesAsync(ct);
            return officer.Id;
        }

        // ------------------------------------------------------------------------------------
        // Centre build (runs once per centre, inside one transaction)
        // ------------------------------------------------------------------------------------

        private sealed class TermRef
        {
            public required TxYearlyAndTermlyStatus Row { get; init; }
            public required int TermIndex { get; init; }
            public required DateTime Start { get; init; }
            public required DateTime End { get; init; }
            public required bool IsCurrent { get; init; }
        }

        private static async Task<string> EnsureCentreAsync(
            AppDbContext db,
            CentreWorld world,
            string commissionUserId,
            IReadOnlyDictionary<string, string> roleIds,
            string passwordHash,
            SeedReport report,
            CancellationToken ct)
        {
            var b = world.Blueprint;
            var registrarUserName = $"{b.Slug}.admin";

            var existing = await db.Users.AsNoTracking()
                .Where(u => u.UserName == registrarUserName)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (existing != null && await db.ProgrammeCentreProfiles.AnyAsync(p => p.SchoolInfoId == existing, ct))
            {
                return existing;
            }

            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"Centre '{b.Slug}' exists without a programme profile: an earlier seed run stopped part-way. "
                    + "Remove that centre's rows (or restore the database) before seeding again.");
            }

            var strategy = db.Database.CreateExecutionStrategy();
            var schoolId = string.Empty;
            var counts = (Learners: 0, Facilitators: 0, Scores: 0, LearnerMarks: 0, FacilitatorMarks: 0);

            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                (schoolId, counts) = await BuildCentreAsync(db, world, commissionUserId, roleIds, passwordHash, ct);
                await transaction.CommitAsync(ct);
            });

            report.Learners += counts.Learners;
            report.Facilitators += counts.Facilitators;
            report.AssessmentRows += counts.Scores;
            report.LearnerAttendanceRows += counts.LearnerMarks;
            report.FacilitatorAttendanceRows += counts.FacilitatorMarks;
            return schoolId;
        }

        private static async Task<(string SchoolId, (int, int, int, int, int) Counts)> BuildCentreAsync(
            AppDbContext db,
            CentreWorld world,
            string commissionUserId,
            IReadOnlyDictionary<string, string> roleIds,
            string passwordHash,
            CancellationToken ct)
        {
            var b = world.Blueprint;
            var now = DateTime.UtcNow;
            var address = $"{b.Community}, {b.LgaName} LGA, {b.StateName} State";

            // --- Centre account, school record, licence -------------------------------------
            var registrar = NewUser($"{b.Slug}.admin", "Centre", "Administrator", Truncate(b.Community, 20),
                Gender.Male, UserStatus.SchoolAdmin, passwordHash, Phone(b.Slug + "#office"));
            db.Users.Add(registrar);
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = registrar.Id, RoleId = roleIds["Registry"] });
            var schoolId = registrar.Id;
            await db.SaveChangesAsync(ct);

            db.Set<SchoolRegistrationOfficer>().Add(new SchoolRegistrationOfficer
            {
                UserId = schoolId,
                FullAddress = address,
                SignatureImageString = string.Empty,
                TimeStatus = TimeStatus.Current,
                AccessStatus = AccessStatus.Active,
                AgentUserId = commissionUserId,
                DateOfBirth = new DateTime(1982, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync(ct);

            var nextSsNo = (short)(await db.SchoolInfos.CountAsync(ct) + 1);
            db.SchoolInfos.Add(new SchoolTyInfo
            {
                SchoolRegistrationOfficerId = schoolId,
                SsNo = nextSsNo,
                SchoolName = b.Name,
                SchoolAddress = address,
                Telephone = Phone(b.Slug + "#office"),
                PoAddress = $"P.M.B. {1000 + nextSsNo}, {b.Community}",
                EmailAddress = $"{b.Slug}@almajiri.demo",
                Website = "ourschoolonline.ng",
                SchoolLogoString = string.Empty,
                Motto = b.Kind == CentreKind.Tsangaya
                    ? "Knowledge, Character, Skill"
                    : "A second chance to learn",
                SchoolAdminAccountantStatus = SchoolAdminAccountantStatus.None,
                ElectiveAccessStatus = AccessStatus.Active,
                MyStatus = SubscriptionStatus.HasConfirmed,
                SystemImplementationType = string.Empty,
                FeatureImplementationType = string.Empty,
                NetworkImplementationType = string.Empty,
                TotalStudents = b.LearnerCount.ToString(),
            });

            db.Licensings.Add(new LicensTying
            {
                Id = Guid.NewGuid().ToString("N"),
                SchoolSmsId = schoolId,
                SubscriptionId = 1,
                PaymentDate = DateTime.UtcNow.Date,
                ExpiryDate = DateTime.UtcNow.Date.AddYears(3),
                AgentUserId = commissionUserId,
                LicenseCategory = LicenseCategories.ThreeYears,
                Charges = 0f,
                TimeStatus = TimeStatus.Current,
                RenewalConsumptionStatus = RenewalConsumptionStatus.HasAreadyBeenConsummed,
            });

            // --- Subject categories and subjects --------------------------------------------
            var categoryIds = new Dictionary<SubjectStatus, string>();
            foreach (var status in System.Enum.GetValues<SubjectStatus>())
            {
                var id = Guid.NewGuid().ToString();
                categoryIds[status] = id;
                db.SubjectCategories.Add(new SubjectRxCategories
                {
                    Id = id,
                    SubjectStatus = status,
                    SchoolId = schoolId,
                    SubjectStatusLabel = string.Empty,
                });
            }

            var subjects = SubjectNames
                .Select(name => new RxSubject
                {
                    Id = Guid.NewGuid().ToString(),
                    SubjectName = name,
                    TimeStatus = TimeStatus.Current,
                    SubjectCategoryId = categoryIds[SubjectStatus.General],
                })
                .ToList();
            db.Subjects.AddRange(subjects);

            // --- Head, sessions, terms -------------------------------------------------------
            var headId = Guid.NewGuid().ToString();
            db.PrincipalDetails.Add(new SchoolHeadTx
            {
                Id = headId,
                HeadsName = Truncate(b.HeadName, 40),
                PhoneNumber = Phone(b.Slug + "#head"),
                SignatureImageString = string.Empty,
                Status = TimeStatus.Current,
                SchoolInfoId = schoolId,
                SchoolHeadCategoryId = 1,
                Gender = b.HeadName.StartsWith("Malama", StringComparison.Ordinal) ? Gender.Female : Gender.Male,
            });

            var archiveSession = new TxAcademicSession
            {
                Id = Guid.NewGuid().ToString(),
                Session = ArchiveSession,
                Status = TimeStatus.Previous,
                SchoolInfoId = schoolId,
            };
            var currentSession = new TxAcademicSession
            {
                Id = Guid.NewGuid().ToString(),
                Session = CurrentSession,
                Status = TimeStatus.Current,
                SchoolInfoId = schoolId,
            };
            db.AcademicSessions.AddRange(archiveSession, currentSession);

            var terms = new List<TermRef>();
            for (var i = 0; i < ArchiveTerms.Length; i++)
            {
                var (termStatus, start, end) = ArchiveTerms[i];
                var nextStart = i + 1 < ArchiveTerms.Length ? ArchiveTerms[i + 1].Start : CurrentTermStart;
                terms.Add(new TermRef
                {
                    Row = NewTerm(termStatus, TermlyAccessmentStatus.Previous, archiveSession.Id, headId,
                        SchoolCalendar.SchoolDaysBetween(start, end).Count, nextStart, end, start),
                    TermIndex = i,
                    Start = start,
                    End = end,
                    IsCurrent = false,
                });
            }

            var currentTerm = new TermRef
            {
                Row = NewTerm(TermStatus.First, TermlyAccessmentStatus.Current, currentSession.Id, headId,
                    0, NextTermBegins, CurrentTermEnd.AddDays(-7), CurrentTermStart),
                TermIndex = 3,
                Start = CurrentTermStart,
                End = CurrentTermEnd,
                IsCurrent = true,
            };
            terms.Add(currentTerm);
            db.YearlyAndTermlyAssessmentStatus.AddRange(terms.Select(term => term.Row));

            // --- Levels (classes) and arms ---------------------------------------------------
            var levelLabel = b.Kind == CentreKind.Tsangaya ? "Tsangaya Level" : "Learning Level";
            var classByLevel = new Dictionary<int, RxClasss>();
            var armByLevel = new Dictionary<int, AxClassArm>();
            foreach (var (level, className) in new[] { (1, CurrentClass.Primary1), (2, CurrentClass.Primary2), (3, CurrentClass.Primary3) })
            {
                var classRow = new RxClasss
                {
                    Id = Guid.NewGuid().ToString(),
                    SNo = (byte)level,
                    ClassName = className,
                    ClassLabel = $"{levelLabel} {level}",
                    SchoolInfoId = schoolId,
                };
                classByLevel[level] = classRow;
                db.Classes.Add(classRow);

                var arm = new AxClassArm
                {
                    Id = Guid.NewGuid().ToString(),
                    SNo = 1,
                    ClassId = classRow.Id,
                    ArmName = ArmName.A,
                    FormMaster = string.Empty,
                };
                armByLevel[level] = arm;
                db.ClassArms.Add(arm);
            }

            await db.SaveChangesAsync(ct);

            // --- Facilitators ----------------------------------------------------------------
            var employees = new List<(FacilitatorPlan Plan, TxEmployee Row)>();
            foreach (var plan in world.Facilitators)
            {
                var user = NewUser(plan.UserName, plan.FirstName, plan.MiddleName, plan.SurName, plan.Gender,
                    UserStatus.Teacher, passwordHash, Phone(plan.UserName));
                db.Users.Add(user);
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleIds["Teacher"] });

                var employee = new TxEmployee
                {
                    Id = Guid.NewGuid().ToString(),
                    FullAddress = address,
                    UserId = user.Id,
                    SignatureImageString = string.Empty,
                    TimeStatus = TimeStatus.Current,
                    EmployeeStatus = EmployeeStatus.Teacher,
                    SchoolId = schoolId,
                    EmployeeIncomeStructureStatus = EmployeeIncomeStructureStatus.HasNotSet,
                    EmployeeIdNo = $"{b.StateCode}{b.Code}-F{plan.Index + 1:00}",
                    Decipline = plan.Discipline,
                    EmployeeQualificationStatus = plan.Qualification,
                    DateOfBirth = DateTime.SpecifyKind(plan.DateOfBirth, DateTimeKind.Utc),
                };
                employees.Add((plan, employee));
                db.ClassTeachers.Add(employee);
            }

            // Form masters: the first three facilitators take one level each.
            foreach (var level in new[] { 1, 2, 3 })
            {
                var formMaster = employees[(level - 1) % employees.Count].Row;
                var arm = armByLevel[level];
                arm.FormMaster = formMaster.Id;
                db.ClassArms.Update(arm);
                db.TeacherClassArmAssignments.Add(new TeacherClassArmTxAssignment
                {
                    Id = Guid.NewGuid().ToString(),
                    ArmId = arm.Id,
                    EmployeeId = formMaster.Id,
                    ArmTermlyAssessmentStatus = ArmTermlyAssessmentStatus.Uncompleted,
                });
            }

            var teacherBySubject = new Dictionary<int, TxEmployee>();
            foreach (var (plan, row) in employees)
            {
                foreach (var subjectIndex in plan.Subjects)
                {
                    teacherBySubject.TryAdd(subjectIndex, row);
                }
            }

            foreach (var arm in armByLevel.Values)
            {
                for (var s = 0; s < subjects.Count; s++)
                {
                    db.TeacherSubjectArmAssignments.Add(new TeachTxerSubjectArmAssignment
                    {
                        Id = Guid.NewGuid().ToString(),
                        ArmId = arm.Id,
                        SubjectId = subjects[s].Id,
                        EmployeeId = teacherBySubject[s].Id,
                    });
                }
            }

            await db.SaveChangesAsync(ct);

            // --- Learners --------------------------------------------------------------------
            var archiveFirstTermId = terms[0].Row.Id;
            var learnerRows = new List<(LearnerPlan Plan, TxStudentDetail Row)>();
            foreach (var plan in world.Learners)
            {
                var isOnRoll = plan.Fate == LearnerFate.Active;
                var user = NewUser(plan.UserName, plan.FirstName, plan.MiddleName, plan.SurName, plan.Gender,
                    UserStatus.Student, passwordHash, null);
                user.AccessStatus = isOnRoll ? AccessStatus.Active : AccessStatus.Disable;
                db.Users.Add(user);
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleIds["StudentOrParent"] });

                var guardian = BuildGuardian(b, plan);
                var classRow = classByLevel[plan.Level];
                var learner = new TxStudentDetail
                {
                    UserId = user.Id,
                    SNo = (short)plan.Sequence,
                    IdNo = $"{b.StateCode}/{b.Code}/{plan.Admission:yy}/{plan.Sequence:0000}",
                    DateOfBirth = DateTime.SpecifyKind(plan.DateOfBirth, DateTimeKind.Utc),
                    PlaceOfBirth = Truncate(
                        string.IsNullOrWhiteSpace(plan.OriginLgaName)
                            ? world.StateName(plan.OriginStateId)
                            : $"{plan.OriginLgaName}, {world.StateName(plan.OriginStateId)}",
                        50),
                    Religion = Religion.Islam,
                    BloodGroup = string.Empty,
                    DateOfAdmsn = DateTime.SpecifyKind(plan.Admission, DateTimeKind.Utc),
                    DateOfDischarge = plan.Fate switch
                    {
                        LearnerFate.Graduated => DateTime.SpecifyKind(ArchiveTerms[2].End, DateTimeKind.Utc),
                        LearnerFate.WithdrawnMidYear => DateTime.SpecifyKind(MidYearWithdrawalDate, DateTimeKind.Utc),
                        _ => null,
                    },
                    TransferedFromWhere = string.Empty,
                    // Every learner joined at Level 1, whichever year that was.
                    StartClass = classByLevel[1].ClassLabel,
                    CurrentClass = classRow.ClassName,
                    Passport = string.Empty,
                    ClassArmId = armByLevel[plan.Level].Id,
                    GuardiansName = Truncate(guardian.Name, 50),
                    Relationship = guardian.Relationship,
                    GuardianAddress = Truncate(guardian.Address, 200),
                    Occupation = guardian.Occupation,
                    MobileNumber = guardian.Phone,
                    HomeNumber = guardian.SecondPhone,
                    EmailAddress = string.Empty,
                    MailingAddress = string.Empty,
                    Country = "Nigeria",
                    TimeStatus = isOnRoll ? TimeStatus.Current : TimeStatus.Previous,
                    SchoolInfoId = schoolId,
                    ClassCategory = ClassCategoryStatus.Primary,
                    Status = plan.Fate != LearnerFate.Active
                        ? StudentStatus.Previous
                        : plan.ArchiveLevel == 0 ? StudentStatus.Fresher : StudentStatus.Returning,
                    StudentInitialRegStatus = plan.ArchiveLevel == 0 && plan.Fate == LearnerFate.Active
                        ? StudentInitialRegistrationStatus.Fresher
                        : StudentInitialRegistrationStatus.Returning,
                    AdmisnTermId = plan.ArchiveLevel == 0 && plan.Fate == LearnerFate.Active
                        ? currentTerm.Row.Id
                        : archiveFirstTermId,
                };
                learnerRows.Add((plan, learner));
                db.StudentsDetails.Add(learner);

                db.ProgrammeLearnerProfiles.Add(BuildLearnerProfile(world, plan, user.Id, schoolId));
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            // --- Per-term records: history, scores, affective, registers, report comments ----
            var scoreCount = 0;
            var learnerMarkCount = 0;
            var facilitatorMarkCount = 0;

            foreach (var term in terms)
            {
                var termDays = SchoolCalendar.SchoolDaysBetween(term.Start, term.IsCurrent ? Today() : term.End)
                    .Where(day => day <= term.End && !IsStalled(b, day))
                    .ToList();

                foreach (var (plan, learner) in learnerRows)
                {
                    var level = LevelInTerm(plan, term);
                    if (level == 0)
                    {
                        continue;
                    }

                    var arm = armByLevel[level];

                    db.StudentAcademicProgressHistories.Add(new StudentAcademicTxProgressHistory
                    {
                        Id = Guid.NewGuid().ToString(),
                        StudentId = learner.UserId,
                        YearlyAndTermlyStatusId = term.Row.Id,
                        ClassId = classByLevel[level].Id,
                        ClassCategory = ClassCategoryStatus.Primary,
                    });

                    var marks = BuildLearnerMarks(plan, term, termDays);
                    foreach (var (day, status) in marks)
                    {
                        db.LearnerDailyAttendances.Add(new LearnerDailyAttendance
                        {
                            Id = Guid.NewGuid().ToString(),
                            AttendanceDate = day,
                            Status = status,
                            Remark = RemarkFor(plan, day, status),
                            MarkedByUserId = arm.FormMaster,
                            MarkedAtUtc = DateTime.SpecifyKind(day.AddHours(8.5), DateTimeKind.Utc),
                            SchoolInfoId = schoolId,
                            StudentId = learner.UserId,
                            ClassArmId = arm.Id,
                            YearlyAndTermlyStatusId = term.Row.Id,
                        });
                    }

                    learnerMarkCount += marks.Count;

                    // Withdrawn learners sit the first term only; current-term exams are not yet written.
                    var assessed = !(plan.Fate == LearnerFate.WithdrawnMidYear && term.TermIndex > 0);
                    var totals = new List<double>();
                    if (assessed)
                    {
                        for (var s = 0; s < subjects.Count; s++)
                        {
                            var score = BuildScore(b, plan, s, term, level);
                            db.StudentAccessments.Add(new StudenXtAccessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                LastUpdateDate = DateTime.SpecifyKind(
                                    term.IsCurrent ? Today().AddDays(-3) : term.End.AddDays(-5), DateTimeKind.Utc),
                                SubjectId = subjects[s].Id,
                                StudentId = learner.UserId,
                                EmployeeId = teacherBySubject[s].Id,
                                ClassArmId = arm.Id,
                                YearlyAndTermlyStatusId = term.Row.Id,
                                Ca1 = score.Ca1,
                                Ca2 = score.Ca2,
                                Ca3 = score.Ca3,
                                Exams = score.Exam,
                            });
                            totals.Add(score.Ca1 + score.Ca2 + score.Ca3 + score.Exam);
                            scoreCount++;
                        }

                        var formMasterId = arm.FormMaster;
                        foreach (var (itemId, _) in AffectiveItems)
                        {
                            db.AffectiveAndPsychomotorAssessments.Add(new AffectiveAndPsychomoTxtorAssessment
                            {
                                Id = Guid.NewGuid().ToString(),
                                ItemId = itemId,
                                Score = AffectiveScore(plan, itemId, term),
                                YearlyAndTermlyStatusId = term.Row.Id,
                                StudentId = learner.UserId,
                                ClassArmId = arm.Id,
                                FormMasterId = formMasterId,
                            });
                        }
                    }

                    var daysPresent = marks.Count(mark => IsCountedPresent(mark.Status));
                    var average = totals.Count == 0 ? 0 : totals.Average();
                    db.StudentsAttendanceReports.Add(new StudentsAtteXzndanceAndCommentsReport
                    {
                        Id = Guid.NewGuid().ToString(),
                        Attendance = (byte)Math.Min(byte.MaxValue, daysPresent),
                        // Comments are written at the end of term; the open term has none yet.
                        TeacherComments = term.IsCurrent || totals.Count == 0
                            ? string.Empty
                            : TeacherComment(plan, average, marks.Count == 0 ? 100 : daysPresent * 100d / marks.Count, term),
                        SchoolHeadComments = term.IsCurrent || totals.Count == 0 ? string.Empty : HeadComment(plan, average),
                        YearlyAndTermlyStatusId = term.Row.Id,
                        StudentId = learner.UserId,
                        ClassArmId = arm.Id,
                    });
                }

                var staffDays = term.IsCurrent ? termDays.Where(day => !IsStaffStalled(b, day)).ToList() : termDays;
                foreach (var (plan, employee) in employees)
                {
                    foreach (var day in staffDays)
                    {
                        db.FacilitatorDailyAttendances.Add(new FacilitatorDailyAttendance
                        {
                            Id = Guid.NewGuid().ToString(),
                            AttendanceDate = day,
                            Status = FacilitatorStatus(plan, day),
                            Remark = string.Empty,
                            MarkedByUserId = schoolId,
                            MarkedAtUtc = DateTime.SpecifyKind(day.AddHours(8), DateTimeKind.Utc),
                            SchoolInfoId = schoolId,
                            EmployeeId = employee.Id,
                            YearlyAndTermlyStatusId = term.Row.Id,
                        });
                    }

                    facilitatorMarkCount += staffDays.Count;
                }

                if (term.IsCurrent)
                {
                    var current = await db.YearlyAndTermlyAssessmentStatus.FirstAsync(row => row.Id == term.Row.Id, ct);
                    current.TotalAttendance = (byte)Math.Min(byte.MaxValue, termDays.Count);
                    db.YearlyAndTermlyAssessmentStatus.Update(current);
                }

                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }

            // --- Programme profile and labels last: their presence marks the centre complete ---
            foreach (var (itemId, label) in ProgrammeAffectiveLabels)
            {
                db.CentreAffectiveItemLabels.Add(new CentreAffectiveItemLabel
                {
                    SchoolInfoId = schoolId,
                    ItemId = itemId,
                    Label = label,
                });
            }

            db.ProgrammeCentreProfiles.Add(new ProgrammeCentreProfile
            {
                SchoolInfoId = schoolId,
                CentreType = b.CentreType,
                StateId = b.StateId,
                LocalGovernmentId = b.LgaId,
                Community = b.Community,
                ProprietorName = b.HeadName,
                YearEstablished = b.YearEstablished,
                OnboardedAtUtc = now,
            });

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            return (schoolId, (world.Learners.Count, world.Facilitators.Count, scoreCount, learnerMarkCount, facilitatorMarkCount));
        }

        /// <summary>
        /// Adds the current term's register for school days since the last run, then refreshes the
        /// term totals the report card prints — the same rule as "Sync to report cards".
        /// </summary>
        private static async Task TopUpCurrentTermAsync(
            AppDbContext db,
            CentreWorld world,
            string schoolId,
            SeedReport report,
            CancellationToken ct)
        {
            var b = world.Blueprint;
            var currentTerm = await db.YearlyAndTermlyAssessmentStatus
                .Include(term => term.Session)
                .FirstOrDefaultAsync(term => term.Session != null
                    && term.Session.SchoolInfoId == schoolId
                    && term.TermlyAccessmentStatus == TermlyAccessmentStatus.Current, ct);
            if (currentTerm == null)
            {
                return;
            }

            var lastDay = Today() > CurrentTermEnd ? CurrentTermEnd : Today();
            var markedDays = (await db.LearnerDailyAttendances.AsNoTracking()
                    .Where(mark => mark.SchoolInfoId == schoolId && mark.YearlyAndTermlyStatusId == currentTerm.Id)
                    .Select(mark => mark.AttendanceDate)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();

            var missingDays = SchoolCalendar.SchoolDaysBetween(CurrentTermStart, lastDay)
                .Where(day => !markedDays.Contains(day) && !IsStalled(b, day))
                .ToList();

            if (missingDays.Count > 0)
            {
                var usernames = world.Learners.Where(p => p.Fate == LearnerFate.Active).Select(p => p.UserName).ToList();
                var learners = await db.Users.AsNoTracking()
                    .Where(u => usernames.Contains(u.UserName!))
                    .Join(db.StudentsDetails.AsNoTracking(), u => u.Id, s => s.UserId,
                        (u, s) => new { u.UserName, s.UserId, s.ClassArmId, s.TimeStatus })
                    .Where(row => row.TimeStatus == TimeStatus.Current)
                    .ToListAsync(ct);
                var planByUserName = world.Learners.ToDictionary(p => p.UserName);

                var armIds = learners.Select(l => l.ClassArmId).Distinct().ToList();
                var formMasters = await db.ClassArms.AsNoTracking()
                    .Where(arm => armIds.Contains(arm.Id))
                    .ToDictionaryAsync(arm => arm.Id, arm => arm.FormMaster, ct);

                var termRef = new TermRef
                {
                    Row = currentTerm,
                    TermIndex = 3,
                    Start = CurrentTermStart,
                    End = CurrentTermEnd,
                    IsCurrent = true,
                };

                foreach (var learner in learners)
                {
                    var plan = planByUserName[learner.UserName!];
                    foreach (var (day, status) in BuildLearnerMarks(plan, termRef, missingDays))
                    {
                        db.LearnerDailyAttendances.Add(new LearnerDailyAttendance
                        {
                            Id = Guid.NewGuid().ToString(),
                            AttendanceDate = day,
                            Status = status,
                            Remark = RemarkFor(plan, day, status),
                            MarkedByUserId = formMasters.GetValueOrDefault(learner.ClassArmId),
                            MarkedAtUtc = DateTime.SpecifyKind(day.AddHours(8.5), DateTimeKind.Utc),
                            SchoolInfoId = schoolId,
                            StudentId = learner.UserId,
                            ClassArmId = learner.ClassArmId,
                            YearlyAndTermlyStatusId = currentTerm.Id,
                        });
                        report.LearnerAttendanceRows++;
                    }
                }

                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }

            // The staff register is kept separately, so it is topped up on its own days.
            var markedStaffDays = (await db.FacilitatorDailyAttendances.AsNoTracking()
                    .Where(mark => mark.SchoolInfoId == schoolId && mark.YearlyAndTermlyStatusId == currentTerm.Id)
                    .Select(mark => mark.AttendanceDate)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();
            var missingStaffDays = SchoolCalendar.SchoolDaysBetween(CurrentTermStart, lastDay)
                .Where(day => !markedStaffDays.Contains(day) && !IsStaffStalled(b, day))
                .ToList();

            if (missingStaffDays.Count > 0)
            {
                var facilitatorNames = world.Facilitators.Select(f => f.UserName).ToList();
                var facilitators = await db.Users.AsNoTracking()
                    .Where(u => facilitatorNames.Contains(u.UserName!))
                    .Join(db.ClassTeachers.AsNoTracking(), u => u.Id, e => e.UserId, (u, e) => new { u.UserName, e.Id })
                    .ToListAsync(ct);
                var facilitatorPlans = world.Facilitators.ToDictionary(f => f.UserName);

                foreach (var facilitator in facilitators)
                {
                    var plan = facilitatorPlans[facilitator.UserName!];
                    foreach (var day in missingStaffDays)
                    {
                        db.FacilitatorDailyAttendances.Add(new FacilitatorDailyAttendance
                        {
                            Id = Guid.NewGuid().ToString(),
                            AttendanceDate = day,
                            Status = FacilitatorStatus(plan, day),
                            Remark = string.Empty,
                            MarkedByUserId = schoolId,
                            MarkedAtUtc = DateTime.SpecifyKind(day.AddHours(8), DateTimeKind.Utc),
                            SchoolInfoId = schoolId,
                            EmployeeId = facilitator.Id,
                            YearlyAndTermlyStatusId = currentTerm.Id,
                        });
                        report.FacilitatorAttendanceRows++;
                    }
                }

                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }

            // Refresh the term totals so the report card agrees with the register.
            var presentByLearner = await db.LearnerDailyAttendances.AsNoTracking()
                .Where(mark => mark.SchoolInfoId == schoolId && mark.YearlyAndTermlyStatusId == currentTerm.Id)
                .GroupBy(mark => mark.StudentId)
                .Select(group => new
                {
                    StudentId = group.Key,
                    Present = group.Count(mark => mark.Status != DailyAttendanceStatus.Absent),
                })
                .ToDictionaryAsync(row => row.StudentId, row => row.Present, ct);

            var reports = await db.StudentsAttendanceReports
                .Where(row => row.YearlyAndTermlyStatusId == currentTerm.Id)
                .ToListAsync(ct);
            foreach (var row in reports)
            {
                if (presentByLearner.TryGetValue(row.StudentId, out var present))
                {
                    row.Attendance = (byte)Math.Min(byte.MaxValue, present);
                    db.StudentsAttendanceReports.Update(row);
                }
            }

            var registeredDays = await db.LearnerDailyAttendances.AsNoTracking()
                .Where(mark => mark.SchoolInfoId == schoolId && mark.YearlyAndTermlyStatusId == currentTerm.Id)
                .Select(mark => mark.AttendanceDate)
                .Distinct()
                .CountAsync(ct);
            var trackedTerm = await db.YearlyAndTermlyAssessmentStatus.FirstAsync(term => term.Id == currentTerm.Id, ct);
            trackedTerm.TotalAttendance = (byte)Math.Min(byte.MaxValue, registeredDays);
            db.YearlyAndTermlyAssessmentStatus.Update(trackedTerm);

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        // ------------------------------------------------------------------------------------
        // Record builders
        // ------------------------------------------------------------------------------------

        private static ApplicationUser NewUser(
            string userName,
            string firstName,
            string middleName,
            string surName,
            Gender gender,
            UserStatus status,
            string passwordHash,
            string? phone)
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@almajiri.demo",
                NormalizedEmail = $"{userName}@almajiri.demo".ToUpperInvariant(),
                EmailConfirmed = true,
                FirstName = Truncate(firstName, 20),
                MiddleName = Truncate(middleName, 20),
                SurName = Truncate(surName, 20),
                Gender = gender,
                CreatedDate = DateTime.UtcNow,
                UserStatus = status,
                AccessStatus = AccessStatus.Active,
                SecurityStamp = Guid.NewGuid().ToString(),
                PasswordHash = passwordHash,
                PhoneNumber = phone,
            };
        }

        private static TxYearlyAndTermlyStatus NewTerm(
            TermStatus term,
            TermlyAccessmentStatus status,
            string sessionId,
            string headId,
            int totalDays,
            DateTime nextTermBegins,
            DateTime assessmentEnd,
            DateTime created)
        {
            return new TxYearlyAndTermlyStatus
            {
                Id = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.SpecifyKind(created, DateTimeKind.Utc),
                TermlyAccessmentStatus = status,
                TermStatus = term,
                CasCategory = CasCategory.ThreeCAs,
                PerCasValue = 10,
                MinPassMark = 40,
                TotalAttendance = (byte)Math.Min(byte.MaxValue, totalDays),
                NextTermBeginsDate = DateTime.SpecifyKind(nextTermBegins, DateTimeKind.Utc),
                AssessmtEndDate = DateTime.SpecifyKind(assessmentEnd, DateTimeKind.Utc),
                SessionId = sessionId,
                HeadmasterId = headId,
                PrincipalId = headId,
            };
        }

        private sealed record Guardian(
            string Name,
            string Relationship,
            string Address,
            string Occupation,
            string Phone,
            string SecondPhone);

        private static Guardian BuildGuardian(CentreBlueprint b, LearnerPlan plan)
        {
            var key = plan.UserName;
            var parentPhone = Phone(key + "#parent");

            // An Almajiri's day-to-day guardian is the Malam of his Tsangaya; the family is far away.
            if (plan.Category == LearnerProgrammeCategory.Almajiri && plan.Tsangaya.Length > 0)
            {
                var malam = plan.Tsangaya.Replace("Tsangayar ", string.Empty, StringComparison.Ordinal);
                return new Guardian(
                    malam,
                    "Tsangaya Malam",
                    $"{plan.Tsangaya}, {b.Community}, {b.StateName} State",
                    "Qur'anic teacher",
                    Phone(b.Slug + "#" + plan.Tsangaya),
                    parentPhone);
            }

            var roll = Unit(key + "#relationship");
            var family = $"{b.Community}, {b.LgaName} LGA, {b.StateName} State";
            if (roll < 0.45)
            {
                return new Guardian($"Malam {plan.MiddleName} {plan.SurName}", "Father", family,
                    Pick(GuardianOccupations, key + "#occupation"), parentPhone, string.Empty);
            }

            var femalePool = b.StateId is 8 or 36 ? KanuriFemaleFirstNames : FemaleFirstNames;
            if (roll < 0.82)
            {
                return new Guardian($"Malama {Pick(femalePool, key + "#mother")} {plan.SurName}", "Mother", family,
                    Pick(MotherOccupations, key + "#occupation"), parentPhone, string.Empty);
            }

            if (roll < 0.94)
            {
                return new Guardian($"Malam {Pick(MaleFirstNames, key + "#uncle")} {plan.SurName}", "Uncle", family,
                    Pick(GuardianOccupations, key + "#occupation"), parentPhone, string.Empty);
            }

            return new Guardian($"Hajiya {Pick(femalePool, key + "#grandmother")} {plan.SurName}", "Grandmother", family,
                "Petty trader", parentPhone, string.Empty);
        }

        private static ProgrammeLearnerProfile BuildLearnerProfile(
            CentreWorld world,
            LearnerPlan plan,
            string studentId,
            string schoolId)
        {
            var b = world.Blueprint;
            var key = plan.UserName;
            var ageYears = (new DateTime(2026, 10, 1) - plan.DateOfBirth).TotalDays / 365.25;

            byte? juz = plan.Category switch
            {
                LearnerProgrammeCategory.Almajiri =>
                    (byte)Math.Clamp(Math.Round((ageYears - 6) * 2.2 + Normal(key + "#juz") * 2.5), 1, 30),
                _ when Unit(key + "#islamiyya") < 0.5 =>
                    (byte)Math.Clamp(Math.Round((ageYears - 7) * 0.8 + Normal(key + "#juz") * 1.2), 1, 10),
                _ => null,
            };

            var prior = plan.Category switch
            {
                LearnerProgrammeCategory.Almajiri => "Qur'anic school (Tsangaya) only",
                LearnerProgrammeCategory.OutOfSchoolDroppedOut =>
                    $"Left Primary {2 + (int)(Unit(key + "#prior") * 3)} in {2021 + (int)(Unit(key + "#prioryear") * 4)}",
                LearnerProgrammeCategory.DisplacedChild => "Schooling interrupted by displacement",
                _ => "Never enrolled",
            };

            var profile = new ProgrammeLearnerProfile
            {
                StudentId = studentId,
                SchoolInfoId = schoolId,
                Category = plan.Category,
                StateOfOriginId = plan.OriginStateId,
                LocalGovernmentOfOriginId = plan.OriginLgaId,
                Tsangaya = plan.Tsangaya,
                QuranJuzMemorised = juz,
                PriorSchooling = prior,
                TransitionStatus = LearnerTransitionStatus.Active,
                TransitionDestination = string.Empty,
            };

            if (plan.Fate == LearnerFate.Graduated)
            {
                var roll = Unit(key + "#transition");
                profile.TransitionDate = DateTime.SpecifyKind(ArchiveTerms[2].End, DateTimeKind.Utc);
                if (roll < 0.62)
                {
                    profile.TransitionStatus = LearnerTransitionStatus.TransitionedToFormalSchool;
                    profile.TransitionDestination = ageYears >= 12.5
                        ? $"JSS 1, Government Junior Secondary School, {b.Community}"
                        : $"Primary 5, LEA Primary School, {b.Community}";
                }
                else if (roll < 0.88)
                {
                    profile.TransitionStatus = LearnerTransitionStatus.TransitionedToVocational;
                    var trade = Pick(plan.Gender == Gender.Female ? GirlsTrades : BoysTrades, key + "#trade");
                    profile.TransitionDestination = $"{trade} apprenticeship, {b.Community} Skills Acquisition Centre";
                }
                else
                {
                    profile.TransitionStatus = LearnerTransitionStatus.Withdrawn;
                    profile.TransitionDestination = $"Returned to family in {world.StateName(plan.OriginStateId)} State";
                }
            }
            else if (plan.Fate == LearnerFate.WithdrawnMidYear)
            {
                profile.TransitionStatus = LearnerTransitionStatus.Withdrawn;
                profile.TransitionDate = DateTime.SpecifyKind(MidYearWithdrawalDate, DateTimeKind.Utc);
                profile.TransitionDestination = Unit(key + "#why") < 0.5
                    ? "Family relocated; no follow-up school recorded"
                    : "Withdrawn by guardian to work on the family farm";
            }

            return profile;
        }

        /// <summary>The level a learner sat in a given term, or 0 if they were not on roll.</summary>
        private static int LevelInTerm(LearnerPlan plan, TermRef term)
        {
            if (term.IsCurrent)
            {
                return plan.Fate == LearnerFate.Active ? plan.Level : 0;
            }

            if (plan.ArchiveLevel == 0)
            {
                return 0;
            }

            if (plan.Fate == LearnerFate.WithdrawnMidYear && term.TermIndex > 1)
            {
                return 0;
            }

            return plan.ArchiveLevel;
        }

        private static bool IsStalled(CentreBlueprint b, DateTime day)
            => b.Slug == "sokoto-wamakko" && day > StalledRegisterAfter;

        /// <summary>The staff register stalls wherever the class register does, and at Bauchi on its own.</summary>
        private static bool IsStaffStalled(CentreBlueprint b, DateTime day)
            => IsStalled(b, day) || (b.Slug == "bauchi-bauchi" && day > StalledStaffRegisterAfter);

        private static DateTime Today() => DateTime.Today;

        private static List<(DateTime Day, DailyAttendanceStatus Status)> BuildLearnerMarks(
            LearnerPlan plan,
            TermRef term,
            IReadOnlyList<DateTime> days)
        {
            var marks = new List<(DateTime, DailyAttendanceStatus)>();

            // Harvest season (mid third term) pulls rural attendance down a little.
            foreach (var day in days)
            {
                if (day < plan.Admission.Date)
                {
                    continue;
                }

                if (plan.Fate == LearnerFate.WithdrawnMidYear && day > MidYearWithdrawalDate)
                {
                    continue;
                }

                var key = $"{plan.UserName}|{day:yyyyMMdd}";

                if (plan.DropOutRisk && term.IsCurrent && day >= DropOutStreakFrom)
                {
                    marks.Add((day, DailyAttendanceStatus.Absent));
                    continue;
                }

                // The register warned before a mid-year withdrawal: a run of absences first.
                if (plan.Fate == LearnerFate.WithdrawnMidYear && day > MidYearWithdrawalDate.AddDays(-14))
                {
                    marks.Add((day, Unit(key + "#w") < 0.85 ? DailyAttendanceStatus.Absent : DailyAttendanceStatus.Late));
                    continue;
                }

                var seasonal = term.TermIndex == 2 && day.Month is 6 or 7 ? 0.95 : 1.0;
                var presentChance = plan.AttendancePropensity * seasonal;
                var roll = Unit(key);
                if (roll < presentChance)
                {
                    marks.Add((day, DailyAttendanceStatus.Present));
                    continue;
                }

                var remainder = (roll - presentChance) / Math.Max(1 - presentChance, 0.0001);
                marks.Add((day, remainder switch
                {
                    < 0.6 => DailyAttendanceStatus.Absent,
                    < 0.85 => DailyAttendanceStatus.Late,
                    _ => DailyAttendanceStatus.Excused,
                }));
            }

            return marks;
        }

        private static string RemarkFor(LearnerPlan plan, DateTime day, DailyAttendanceStatus status)
        {
            var key = $"{plan.UserName}|{day:yyyyMMdd}#remark";
            return status switch
            {
                DailyAttendanceStatus.Excused => Pick(
                    new[] { "Excused by guardian", "Sick; Malam informed", "Family event, excused" }, key),
                DailyAttendanceStatus.Absent when plan.DropOutRisk && day >= DropOutStreakFrom =>
                    Unit(key) < 0.3 ? "Absent again; facilitator to follow up with guardian" : string.Empty,
                DailyAttendanceStatus.Late => Unit(key) < 0.25 ? "Arrived after first lesson" : string.Empty,
                _ => string.Empty,
            };
        }

        private static DailyAttendanceStatus FacilitatorStatus(FacilitatorPlan plan, DateTime day)
        {
            var roll = Unit($"{plan.UserName}|{day:yyyyMMdd}");
            if (roll < plan.AttendancePropensity)
            {
                return DailyAttendanceStatus.Present;
            }

            return roll < plan.AttendancePropensity + (1 - plan.AttendancePropensity) * 0.15
                ? DailyAttendanceStatus.Late
                : DailyAttendanceStatus.Absent;
        }

        private sealed record Score(float Ca1, float Ca2, float Ca3, float Exam);

        private static Score BuildScore(CentreBlueprint b, LearnerPlan plan, int subjectIndex, TermRef term, int level)
        {
            var key = $"{plan.UserName}|s{subjectIndex}";
            var termKey = $"{key}|t{term.TermIndex}";

            var percent = SubjectBaseline[b.Kind][subjectIndex]
                + plan.Ability * 11
                + Normal(key + "#affinity") * 6
                + term.TermIndex * 1.5
                + (level - 1) * 1.5
                + (b.LearnerAttendanceBase - 0.86) * 45
                + Normal(termKey + "#noise") * 4
                - (plan.DropOutRisk ? 6 : 0);
            percent = Math.Clamp(percent, 16, 97);

            float Ca(string part) => (float)Math.Clamp(Math.Round(percent / 10 + Normal(termKey + part) * 1.1), 0, 10);

            var ca1 = Ca("#ca1");
            var ca2 = Ca("#ca2");

            // Seven weeks into the open term: two CAs are in, CA3 and the examination are still to come.
            if (term.IsCurrent)
            {
                return new Score(ca1, ca2, 0, 0);
            }

            var ca3 = Ca("#ca3");
            var exam = (float)Math.Clamp(Math.Round(percent * 0.7 + Normal(termKey + "#exam") * 4), 0, 70);
            return new Score(ca1, ca2, ca3, exam);
        }

        private static byte AffectiveScore(LearnerPlan plan, byte itemId, TermRef term)
        {
            var value = 3.4 + plan.Ability * 0.6 + Normal($"{plan.UserName}|aff{itemId}|t{term.TermIndex}") * 0.7;
            return (byte)Math.Clamp(Math.Round(value), 1, 5);
        }

        private static string TeacherComment(LearnerPlan plan, double average, double attendanceRate, TermRef term)
        {
            var name = plan.FirstName;
            var key = $"{plan.UserName}|t{term.TermIndex}#comment";

            // Attendance is the first thing to address when it is the problem.
            if (attendanceRate < 80)
            {
                return Truncate(Pick(new[]
                {
                    $"{name} missed too many days this term. Daily attendance must improve.",
                    $"Irregular attendance is holding {name} back. Guardian to ensure daily attendance.",
                }, key), 150);
            }

            string[] options = average switch
            {
                >= 70 =>
                [
                    $"Excellent term. {name} is attentive and helps classmates with reading.",
                    $"Outstanding effort. {name} now reads fluently in Hausa and English.",
                    $"A very good term. {name} is disciplined and eager to learn.",
                ],
                >= 60 =>
                [
                    $"Very good progress. {name} should keep practising numeracy.",
                    $"Good, steady effort. {name} takes part well in class.",
                    $"A good term. More English reading practice will help {name}.",
                ],
                >= 50 =>
                [
                    $"Fair performance. {name} needs to improve in English Literacy and Numeracy.",
                    $"Steady progress. {name} should revise daily with the facilitator.",
                    $"An average term. {name} can do better in reading and writing.",
                ],
                >= 40 =>
                [
                    $"{name} passed but needs extra support in English Literacy and Numeracy.",
                    $"{name} attends well but is still building basic literacy. Keep encouraging.",
                    $"A modest term. Remedial reading sessions will help {name}.",
                ],
                _ =>
                [
                    $"{name} needs close support. Remedial literacy lessons are recommended.",
                    $"Below expectation. {name} has been placed in remedial lessons.",
                    $"Weak term. The facilitator will work closely with {name} next term.",
                ],
            };

            return Truncate(Pick(options, key), 150);
        }

        private static string HeadComment(LearnerPlan plan, double average)
        {
            var key = plan.UserName + "#head";
            string[] options = average switch
            {
                >= 70 => ["Excellent result. Keep it up.", "A commendable performance. Well done."],
                >= 60 => ["Good result. Aim higher next term.", "Good work. Keep improving."],
                >= 50 => ["Fair result. Work harder next term.", "Satisfactory. More effort is needed."],
                >= 40 => ["A pass. Extra support has been arranged.", "Passed. Keep working hard."],
                _ => ["Needs improvement. Remedial support has been arranged.", "Poor result. Guardian to see the centre head."],
            };

            return Pick(options, key);
        }

        private static bool IsCountedPresent(DailyAttendanceStatus status)
            => status is DailyAttendanceStatus.Present or DailyAttendanceStatus.Late or DailyAttendanceStatus.Excused;

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];
    }
}
