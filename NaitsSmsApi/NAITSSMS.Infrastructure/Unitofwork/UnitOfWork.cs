using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NAITSSMS.Infrastructure.Repo;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Unitofwork
{
    public sealed class UnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly AppDbContext _context;
        private readonly ConcurrentDictionary<object, object> _repositories = new();
        private IDbContextTransaction? _currentTransaction;

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool HasActiveTransaction => _currentTransaction != null;

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            var key = (typeof(IGenericRepository<>), typeof(TEntity));
            if (_repositories.TryGetValue(key, out var repo))
                return (IGenericRepository<TEntity>)repo;

            var instance = new GenericRepository<TEntity>(_context);
            _repositories[key] = instance;
            return instance;
        }

        public IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class
        {
            var key = (typeof(IGenericRepository<,>), typeof(TEntity), typeof(TKey));
            if (_repositories.TryGetValue(key, out var repo))
                return (IGenericRepository<TEntity, TKey>)repo;

            var instance = new GenericRepository<TEntity, TKey>(_context);
            _repositories[key] = instance;
            return instance;
        }

        public int SaveChanges() => _context.SaveChanges();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
                return;

            // NpgsqlRetryingExecutionStrategy rejects BeginTransaction outside an execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                if (_currentTransaction == null)
                    _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            });
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null) return;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_currentTransaction == null) return;

            try { await _currentTransaction.RollbackAsync(); } catch { /* ignore */ }
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;

            RejectChanges();
        }

        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            return ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            }, cancellationToken);
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                _currentTransaction = transaction;
                try
                {
                    var result = await operation();
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    try { await transaction.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                    RejectChanges();
                    throw;
                }
                finally
                {
                    if (ReferenceEquals(_currentTransaction, transaction))
                        _currentTransaction = null;
                }
            });
        }

        public void RejectChanges()
        {
            foreach (var entry in _context.ChangeTracker.Entries())
            {
                switch (entry.State)
                {
                    case EntityState.Modified:
                        entry.State = EntityState.Unchanged;
                        break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Deleted:
                        entry.State = EntityState.Unchanged;
                        break;
                }
            }
        }

        [Obsolete("Use BeginTransactionAsync instead.")]
        public void BeginTransaction()
        {
            if (_currentTransaction != null)
                return;

            var strategy = _context.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                if (_currentTransaction == null)
                    _currentTransaction = _context.Database.BeginTransaction();
            });
        }

        [Obsolete("Use CommitTransactionAsync instead.")]
        public bool Commit()
        {
            if (_currentTransaction != null)
            {
                _context.SaveChanges();
                _currentTransaction.Commit();
                _currentTransaction.Dispose();
                _currentTransaction = null;
                return true;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
                using var tx = _context.Database.BeginTransaction();
                try
                {
                    _context.SaveChanges();
                    tx.Commit();
                    return true;
                }
                catch
                {
                    try { tx.Rollback(); } catch { /* ignore */ }
                    RejectChanges();
                    throw;
                }
            });
        }

        [Obsolete("Use RollbackTransactionAsync instead.")]
        public void Rollback()
        {
            if (_currentTransaction != null)
            {
                try { _currentTransaction.Rollback(); } catch { /* ignore */ }
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
            RejectChanges();
        }

        [Obsolete("EF Core manages connections; you typically don't need this.")]
        public bool CloseConnection()
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Closed)
                conn.Close();
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
            // DbContext is disposed by DI
        }
    }
}
