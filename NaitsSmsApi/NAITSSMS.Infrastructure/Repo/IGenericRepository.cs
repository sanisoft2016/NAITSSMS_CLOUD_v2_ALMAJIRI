using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Repo
{
    // Base: strongly-typed PK
    public interface IGenericRepository<T, in TPrimaryKey> where T : class
    {
        void Add(T entity);
        // Update (sync and async)
        void Update(T entity);


        // Delete (by entity instance)
        void Delete(T entity);

        // Delete (by primary key)
        Task<bool> DeleteAsync(TPrimaryKey id, CancellationToken cancellationToken = default);


        // Async reads
        Task<T> FindAsync(TPrimaryKey id, CancellationToken cancellationToken = default);

        Task<T> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes);

        Task<List<T>> GetAllAsync(
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes);

        Task<List<T>> WhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes);

        Task<int> CountAsync(
            Expression<Func<T, bool>> predicate = null,
            CancellationToken cancellationToken = default);

        Task<List<TResult>> SelectWhereAsync<TResult>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TResult>> selector,
            CancellationToken cancellationToken = default);

        Task<TProperty?> MaxOrDefaultAsync<TProperty>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TProperty>> selector,
            CancellationToken cancellationToken = default);
    }

    // Convenience: assumes string PK, extends base and adds helpers
    public interface IGenericRepository<T> : IGenericRepository<T, string> where T : class
    {
        // Existing sync Update is commonly used in your services
        new bool Update(T entity);
        void Add(T entity);
        void AddRange(IEnumerable<T> entities);

        Task<T> GetByIdAsync(object id, CancellationToken cancellationToken = default);

        Task<List<T>> GetAllAsync(
            Expression<Func<T, bool>> Filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> Order = null,
            string IncludeProperties = "",
            CancellationToken cancellationToken = default);

        // Delete (by entity instance; bool for legacy compatibility)
        new bool Delete(T entity);

        // Delete (by id; async)
        Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default);

    }
}