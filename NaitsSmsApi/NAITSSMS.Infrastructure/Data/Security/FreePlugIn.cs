using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NAITSSMS.Infrastructure.Data.Security
{
    public static class FreePlugIn
    {
        // Legacy symmetric key seed (kept for compatibility with existing data)
        private const string KeySeed = "1%3@g!sm M<as& T^te)r?1$4";

        /// <summary>
        /// Optional override for cloud/Linux hosts where DMI/WMI is unavailable.
        /// Set the same value on every API replica so licensing and Sms Api Key stay stable.
        /// </summary>
        private const string UniqueSystemIdEnvName = "NAITS_UNIQUE_SYSTEM_ID";

        public static string Encrypt(string toEncrypt, bool useHashing)
        {
            if (toEncrypt is null) throw new ArgumentNullException(nameof(toEncrypt));

            var inputBytes = Encoding.UTF8.GetBytes(toEncrypt);
            var key = Derive3DesKey(useHashing ? Md5(KeySeed) : Encoding.UTF8.GetBytes(KeySeed));

            using var tdes = new TripleDESCryptoServiceProvider
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            using var encryptor = tdes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Convert.ToBase64String(cipher);
        }

        public static string Decrypt(string cipherString, bool useHashing)
        {
            if (string.IsNullOrWhiteSpace(cipherString)) return "0";

            // Lib.FreePlugIn.Decrypt wraps FromBase64String in try/catch and returns "0" on failure.
            // Query-string binding often turns Base64 '+' into ' '; restore before decode.
            byte[] cipherBytes;
            try
            {
                cipherBytes = Convert.FromBase64String(cipherString);
            }
            catch
            {
                try
                {
                    cipherBytes = Convert.FromBase64String(cipherString.Replace(' ', '+'));
                }
                catch
                {
                    return "0";
                }
            }

            var key = Derive3DesKey(useHashing ? Md5(KeySeed) : Encoding.UTF8.GetBytes(KeySeed));

            using var tdes = new TripleDESCryptoServiceProvider
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            try
            {
                using var decryptor = tdes.CreateDecryptor();
                var plain = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return "0";
            }
        }

        public static string InternalRegistrationKeyEncodingAndExpiryDate(string licenceId)
        {
            if (string.IsNullOrWhiteSpace(licenceId)) return null;

            var today = DateTime.Today;
            var id = licenceId.Trim();
            if (!id.All(char.IsDigit)) return null;

            string token;
            DateTime expires;

            switch (id.Length)
            {
                case 3:
                    token = BuildToken(id, '9', pow: 7, add: 1234);
                    expires = today.AddMonths(6);
                    break;
                case 4:
                    token = BuildToken(id, '8', pow: 8, add: 1191);
                    expires = today.AddYears(1);
                    break;
                case 5:
                    token = BuildToken(id, '7', pow: 9, add: 1201);
                    expires = today.AddYears(2);
                    break;
                case 6:
                    token = BuildToken(id, '6', pow: 7, add: 126);
                    expires = today.AddYears(3);
                    break;
                case 7:
                    token = BuildToken(id, '5', pow: 8, add: 338);
                    expires = today.AddMonths(4);
                    break;
                case 8:
                    token = BuildToken(id, '9', pow: 5, add: 411);
                    expires = today.AddYears(5);
                    break;
                case 9:
                    token = BuildToken(id, '4', pow: 7, add: 765);
                    expires = today.AddYears(1);
                    break;
                default:
                    return null;
            }

            return $"{token},{expires}";
        }

        public static string GetUniqueSystemId()
        {
            try
            {
                // Cloud/Linux override: WMI is Windows-only; containers often lack DMI.
                var configured = Environment.GetEnvironmentVariable(UniqueSystemIdEnvName);
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return NormalizeSystemId(configured);
                }

                // Legacy Lib.FreePlugIn: board.Trim() + cpu.Trim().
                string board;
                string cpu;
                try
                {
                    board = GetBoardSerialNumbers();
                }
                catch
                {
                    board = string.Empty;
                }

                try
                {
                    cpu = GetCpuIds();
                }
                catch
                {
                    cpu = string.Empty;
                }

                var systemId = NormalizeSystemId(board.Trim() + cpu.Trim());
                if (!string.IsNullOrWhiteSpace(systemId))
                {
                    return systemId;
                }

                return NormalizeSystemId(GetFallbackSystemId());
            }
            catch
            {
                return NormalizeSystemId(
                    Environment.GetEnvironmentVariable(UniqueSystemIdEnvName)
                    ?? Environment.MachineName
                    ?? string.Empty);
            }
        }

        /// <summary>
        /// Value shown as Sms Api Key (machine fingerprint). Falls back to the decrypted
        /// stored school key when the host cannot resolve a hardware id.
        /// </summary>
        public static string ResolveDisplayedSmsApiKey(string? storedEncryptedKey)
        {
            var systemId = GetUniqueSystemId();
            if (!string.IsNullOrWhiteSpace(systemId))
            {
                return systemId;
            }

            if (string.IsNullOrWhiteSpace(storedEncryptedKey))
            {
                return string.Empty;
            }

            var decrypted = Decrypt(storedEncryptedKey, true);
            if (!string.IsNullOrWhiteSpace(decrypted) && decrypted != "0")
            {
                return decrypted;
            }

            return storedEncryptedKey;
        }

        private static string GetBoardSerialNumbers()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return SanitizeHardwareValue(WindowsWmiHardwareId.GetBoardSerialNumber());
                }
                catch
                {
                    return string.Empty;
                }
            }

            // Linux / container hosts: DMI board serial (may need privileged access).
            return SanitizeHardwareValue(ReadFirstExistingText(
                "/sys/class/dmi/id/board_serial",
                "/sys/devices/virtual/dmi/id/board_serial",
                "/sys/class/dmi/id/product_serial",
                "/sys/devices/virtual/dmi/id/product_serial"));
        }

        private static string GetCpuIds()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return SanitizeHardwareValue(WindowsWmiHardwareId.GetProcessorId());
                }
                catch
                {
                    return string.Empty;
                }
            }

            // Linux has no Win32 ProcessorId; prefer cpuinfo Serial (e.g. Raspberry Pi),
            // then DMI product UUID as a stable secondary component.
            var cpuSerial = ReadCpuInfoSerial();
            if (!string.IsNullOrWhiteSpace(cpuSerial))
            {
                return SanitizeHardwareValue(cpuSerial);
            }

            return SanitizeHardwareValue(ReadFirstExistingText(
                "/sys/class/dmi/id/product_uuid",
                "/sys/devices/virtual/dmi/id/product_uuid"));
        }

        private static string GetFallbackSystemId()
        {
            var productUuid = SanitizeHardwareValue(ReadFirstExistingText(
                "/sys/class/dmi/id/product_uuid",
                "/sys/devices/virtual/dmi/id/product_uuid"));
            var machineId = SanitizeHardwareValue(ReadFirstExistingText(
                "/etc/machine-id",
                "/var/lib/dbus/machine-id"));

            var combined = (productUuid + machineId).Trim();
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return combined;
            }

            // Last resort so UI never shows a blank Sms Api Key on Linux.
            return Environment.MachineName ?? string.Empty;
        }

        private static string ReadCpuInfoSerial()
        {
            try
            {
                const string path = "/proc/cpuinfo";
                if (!File.Exists(path))
                {
                    return string.Empty;
                }

                foreach (var line in File.ReadLines(path))
                {
                    if (!line.StartsWith("Serial", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var separator = line.IndexOf(':');
                    if (separator < 0)
                    {
                        continue;
                    }

                    return line[(separator + 1)..].Trim();
                }
            }
            catch
            {
                // ignore
            }

            return string.Empty;
        }

        private static string ReadFirstExistingText(params string[] paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var text = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(text) && !IsPlaceholderHardwareValue(text))
                    {
                        return text;
                    }
                }
                catch
                {
                    // Permission denied / missing DMI is common in containers.
                }
            }

            return string.Empty;
        }

        private static bool IsPlaceholderHardwareValue(string value)
        {
            var normalized = value.Trim();
            return normalized.Length == 0
                || normalized.Equals("None", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Not Specified", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Default string", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("System Serial Number", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeHardwareValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || IsPlaceholderHardwareValue(value))
            {
                return string.Empty;
            }

            return value.Replace('/', '^').Replace('\\', '!');
        }

        private static string NormalizeSystemId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Legacy Lib concatenates board+cpu with Trim only — do not keep internal whitespace
            // (spaces break SMS key round-trips when '+' / control chars are normalized away).
            return Regex.Replace(value.Trim(), @"\s+", string.Empty);
        }

        private static byte[] Md5(string input)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        }

        // Ensures the 3DES key size is valid (16 or 24 bytes in .NET for 2-key/3-key 3DES)
        private static byte[] Derive3DesKey(byte[] raw)
        {
            // Accept 16 or 24 bytes; otherwise expand or trim deterministically
            if (raw.Length == 24 || raw.Length == 16) return raw;

            if (raw.Length < 16)
            {
                var expanded = new byte[16];
                Buffer.BlockCopy(raw, 0, expanded, 0, raw.Length);
                // Repeat bytes to fill
                for (int i = raw.Length, j = 0; i < expanded.Length; i++, j = (j + 1) % raw.Length)
                    expanded[i] = raw[j];
                return expanded;
            }

            if (raw.Length > 24)
            {
                var trimmed = new byte[24];
                Buffer.BlockCopy(raw, 0, trimmed, 0, 24);
                return trimmed;
            }

            // raw.Length between 17..24
            var target = raw.Length <= 20 ? 16 : 24;
            var key = new byte[target];
            Buffer.BlockCopy(raw, 0, key, 0, target);
            return key;
        }

        private static string BuildToken(string digits, char seed, int pow, int add)
        {
            var acc = new StringBuilder().Append(seed);
            foreach (var ch in digits)
            {
                var d = (short)(ch - '0');
                var val = (int)Math.Pow(d, pow) + add;
                acc.Append(val);
            }

            var chars = acc.ToString().ToCharArray();
            Array.Sort(chars);
            return new string(chars);
        }
    }
}
