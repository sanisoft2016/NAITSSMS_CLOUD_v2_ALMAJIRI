using System.Collections.Concurrent;
using CustRewardMgtSys.Application.IService;

namespace CustRewardMgtSys.Application.Service
{
    public sealed class TwoFactorLoginTicketService : ITwoFactorLoginTicketService
    {
        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, TicketEntry> _tickets = new(StringComparer.Ordinal);

        public string CreateTicket(string userId, TimeSpan? lifetime = null)
        {
            var ticket = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            _tickets[ticket] = new TicketEntry(userId, DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime));
            return ticket;
        }

        public bool TryGetTicket(string ticket, out string userId)
        {
            userId = string.Empty;
            if (string.IsNullOrWhiteSpace(ticket))
            {
                return false;
            }

            if (!_tickets.TryGetValue(ticket.Trim(), out var entry))
            {
                return false;
            }

            if (entry.ExpiresUtc <= DateTimeOffset.UtcNow)
            {
                _tickets.TryRemove(ticket.Trim(), out _);
                return false;
            }

            userId = entry.UserId;
            return true;
        }

        public void ConsumeTicket(string ticket)
        {
            if (!string.IsNullOrWhiteSpace(ticket))
            {
                _tickets.TryRemove(ticket.Trim(), out _);
            }
        }

        private sealed record TicketEntry(string UserId, DateTimeOffset ExpiresUtc);
    }
}
