using System.Collections.Concurrent;
using CustRewardMgtSys.Application.IService;
using Microsoft.Extensions.Configuration;

namespace CustRewardMgtSys.Application.Service
{
    /// <summary>
    /// In-memory login attempt tracker: 5 failures / 30 minutes per key (IP or username).
    /// </summary>
    public sealed class LoginThrottleService : ILoginThrottleService
    {
        private readonly ConcurrentDictionary<string, AttemptState> _states = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _maxFailedAttempts;
        private readonly TimeSpan _lockoutWindow;

        public LoginThrottleService(IConfiguration configuration)
        {
            _maxFailedAttempts = Math.Max(1, configuration.GetValue("LoginProtection:MaxFailedAttempts", 5));
            var minutes = Math.Max(1, configuration.GetValue("LoginProtection:LockoutMinutes", 30));
            _lockoutWindow = TimeSpan.FromMinutes(minutes);
        }

        public bool IsLockedOut(string key, out TimeSpan? remaining)
        {
            remaining = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var normalized = Normalize(key);
            if (!_states.TryGetValue(normalized, out var state))
            {
                return false;
            }

            lock (state.Sync)
            {
                Prune(state, DateTimeOffset.UtcNow);
                if (state.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow)
                {
                    remaining = lockoutEnd - DateTimeOffset.UtcNow;
                    return true;
                }

                return false;
            }
        }

        public bool RecordFailure(string key, out TimeSpan? remaining)
        {
            remaining = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var normalized = Normalize(key);
            var state = _states.GetOrAdd(normalized, _ => new AttemptState());
            var now = DateTimeOffset.UtcNow;

            lock (state.Sync)
            {
                Prune(state, now);

                if (state.LockoutEndUtc is { } activeLockout && activeLockout > now)
                {
                    remaining = activeLockout - now;
                    return true;
                }

                state.FailureTimesUtc.Add(now);

                if (state.FailureTimesUtc.Count >= _maxFailedAttempts)
                {
                    state.LockoutEndUtc = now.Add(_lockoutWindow);
                    state.FailureTimesUtc.Clear();
                    remaining = _lockoutWindow;
                    return true;
                }

                return false;
            }
        }

        public void Reset(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _states.TryRemove(Normalize(key), out _);
        }

        private void Prune(AttemptState state, DateTimeOffset now)
        {
            if (state.LockoutEndUtc is { } lockoutEnd && lockoutEnd <= now)
            {
                state.LockoutEndUtc = null;
            }

            var cutoff = now - _lockoutWindow;
            state.FailureTimesUtc.RemoveAll(t => t < cutoff);
        }

        private static string Normalize(string key) => key.Trim().ToUpperInvariant();

        private sealed class AttemptState
        {
            public object Sync { get; } = new();
            public List<DateTimeOffset> FailureTimesUtc { get; } = new();
            public DateTimeOffset? LockoutEndUtc { get; set; }
        }
    }
}
