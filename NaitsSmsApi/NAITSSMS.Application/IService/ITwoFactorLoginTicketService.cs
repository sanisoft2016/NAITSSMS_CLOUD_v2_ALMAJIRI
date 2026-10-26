namespace CustRewardMgtSys.Application.IService
{
    public interface ITwoFactorLoginTicketService
    {
        string CreateTicket(string userId, TimeSpan? lifetime = null);
        bool TryGetTicket(string ticket, out string userId);
        void ConsumeTicket(string ticket);
    }
}
