using NAITSSMS.Infrastructure.Repo;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Unitofwork
{
    public interface IUnitOfWork
    {
        // Generic repo factories
        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
        IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class;

        // Persistence
        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Transactions (preferred async API)
        bool HasActiveTransaction { get; }
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync();

        // Convenience: wraps Begin -> work -> Commit with rollback on failure
        // (runs inside DbContext.Database.CreateExecutionStrategy for Npgsql retries)
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
        Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken = default);

        // Change tracker helpers
        void RejectChanges();

        // Legacy/sync APIs (kept for compatibility, discourage use)
        [Obsolete("Use BeginTransactionAsync + CommitTransactionAsync instead.")]
        void BeginTransaction();

        [Obsolete("Use CommitTransactionAsync instead.")]
        bool Commit();

        [Obsolete("Use RollbackTransactionAsync instead.")]
        void Rollback();

        [Obsolete("EF Core manages connections; you typically don't need this.")]
        bool CloseConnection();
    }
}