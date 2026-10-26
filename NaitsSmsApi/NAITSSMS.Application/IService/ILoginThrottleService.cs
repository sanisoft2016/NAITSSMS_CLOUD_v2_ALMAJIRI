namespace CustRewardMgtSys.Application.IService
{
    public interface ILoginThrottleService
    {
        /// <summary>
        /// Returns false when the key is currently locked out.
        /// </summary>
        bool IsLockedOut(string key, out TimeSpan? remaining);

        /// <summary>
        /// Records a failed attempt. Returns true when this failure triggers/extends lockout.
        /// </summary>
        bool RecordFailure(string key, out TimeSpan? remaining);

        /// <summary>
        /// Clears failure history after a successful login.
        /// </summary>
        void Reset(string key);
    }
}
