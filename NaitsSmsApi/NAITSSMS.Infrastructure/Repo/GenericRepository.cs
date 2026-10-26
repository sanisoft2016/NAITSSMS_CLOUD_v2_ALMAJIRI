using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NAITSSMS.Infrastructure.Repo
{
    public class GenericRepository<T, TPrimaryKey> : IGenericRepository<T, TPrimaryKey> where T : class
    {
        protected readonly AppDbContext Context;
        protected readonly DbSet<T> Dbset;

        public GenericRepository(AppDbContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Dbset = Context.Set<T>();
        }

        public virtual void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // FindAsync(predicate) uses AsNoTracking; FindAsync(id) tracks.
            // If a tracked instance with the same key already exists, merge into it
            // instead of Attach (which throws a duplicate-key tracking conflict).
            var local = FindTrackedWithSameKey(entity);
            if (local != null)
            {
                if (!ReferenceEquals(local, entity))
                {
                    MergeScalarProperties(local, entity);
                }

                Context.Entry(local).State = EntityState.Modified;
                return;
            }

            // Not in the tracker: load by primary key (tracked, no disconnected graph),
            // then copy scalars. This avoids Entry(disconnectedWithNavigations) graph conflicts
            // without mutating the caller's navigation properties.
            var keyValues = GetPrimaryKeyValues(entity);
            if (keyValues != null)
            {
                var tracked = Dbset.Find(keyValues);
                if (tracked != null)
                {
                    if (!ReferenceEquals(tracked, entity))
                    {
                        MergeScalarProperties(tracked, entity);
                    }

                    Context.Entry(tracked).State = EntityState.Modified;
                    return;
                }
            }

            // Fallback for rare cases where the row is not found by Find (e.g. not yet inserted).
            ClearNavigationReferences(entity);
            Context.Entry(entity).State = EntityState.Modified;
        }

        private void MergeScalarProperties(T target, T source)
        {
            var targetEntry = Context.Entry(target);
            foreach (var prop in targetEntry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey() || prop.Metadata.IsShadowProperty())
                    continue;

                var clr = prop.Metadata.PropertyInfo;
                if (clr == null)
                    continue;

                var incoming = clr.GetValue(source);
                if (prop.IsModified && Equals(incoming, prop.OriginalValue))
                    continue;

                prop.CurrentValue = incoming;
                prop.IsModified = true;
            }
        }

        private object[]? GetPrimaryKeyValues(T entity)
        {
            var key = Context.Model.FindEntityType(typeof(T))?.FindPrimaryKey();
            if (key == null || key.Properties.Count == 0)
                return null;

            var values = new object[key.Properties.Count];
            for (var i = 0; i < key.Properties.Count; i++)
            {
                var clr = key.Properties[i].PropertyInfo;
                if (clr == null)
                    return null;
                values[i] = clr.GetValue(entity)!;
            }

            return values;
        }

        private void ClearNavigationReferences(T entity)
        {
            var entityType = Context.Model.FindEntityType(typeof(T));
            if (entityType == null)
                return;

            foreach (var navigation in entityType.GetNavigations())
            {
                var clr = navigation.PropertyInfo;
                if (clr == null || !clr.CanWrite)
                    continue;
                clr.SetValue(entity, null);
            }

            foreach (var navigation in entityType.GetSkipNavigations())
            {
                var clr = navigation.PropertyInfo;
                if (clr == null || !clr.CanWrite)
                    continue;
                clr.SetValue(entity, null);
            }
        }

        private T? FindTrackedWithSameKey(T entity)
        {
            var key = Context.Model.FindEntityType(typeof(T))?.FindPrimaryKey();
            if (key == null || key.Properties.Count == 0)
                return null;

            // Do not call Context.Entry(entity) here — if another instance with the same key
            // is already tracked, Entry() itself throws the duplicate-key conflict.
            var incomingValues = new object?[key.Properties.Count];
            for (var i = 0; i < key.Properties.Count; i++)
            {
                var clr = key.Properties[i].PropertyInfo;
                if (clr == null)
                    return null;
                incomingValues[i] = clr.GetValue(entity);
            }

            foreach (var tracked in Context.ChangeTracker.Entries<T>())
            {
                if (tracked.Entity == null)
                    continue;

                var match = true;
                for (var i = 0; i < key.Properties.Count; i++)
                {
                    var trackedValue = tracked.Property(key.Properties[i].Name).CurrentValue;
                    if (!Equals(trackedValue, incomingValues[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return tracked.Entity;
            }

            return null;
        }

        // Delete by entity instance (hard delete)
        public virtual void Delete(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Prefer removing an already-tracked instance. Never Attach() — Attach walks
            // navigation graphs and throws when a related key is already tracked.
            var local = FindTrackedWithSameKey(entity);
            if (local != null)
            {
                Dbset.Remove(local);
                return;
            }

            ClearNavigationReferences(entity);
            Context.Entry(entity).State = EntityState.Deleted;
        }

        // Delete by primary key (hard delete). Returns false if not found.
        public virtual async Task<bool> DeleteAsync(TPrimaryKey id, CancellationToken cancellationToken = default)
        {
            var entity = await Dbset.FindAsync(new object[] { id }, cancellationToken).AsTask();
            if (entity == null) return false;
            Dbset.Remove(entity);
            return true;
        }

        public void Add(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Dbset.Add(entity);
        }

        public Task<T> FindAsync(TPrimaryKey id, CancellationToken cancellationToken = default)
            => Dbset.FindAsync(new object[] { id }, cancellationToken).AsTask();

        public async Task<T> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = Dbset.AsNoTracking();
            if (includes != null && includes.Length > 0)
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            return await query.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<List<T>> GetAllAsync(
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = Dbset.AsNoTracking();
            if (includes != null && includes.Length > 0)
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            return await query.ToListAsync(cancellationToken);
        }

        public async Task<List<T>> WhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = Dbset.AsNoTracking();
            if (includes != null && includes.Length > 0)
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            return await query.Where(predicate).ToListAsync(cancellationToken);
        }

        public Task<int> CountAsync(
            Expression<Func<T, bool>> predicate = null,
            CancellationToken cancellationToken = default)
        {
            return predicate != null
                ? Dbset.CountAsync(predicate, cancellationToken)
                : Dbset.CountAsync(cancellationToken);
        }

        public async Task<List<TResult>> SelectWhereAsync<TResult>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TResult>> selector,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return await Dbset.AsNoTracking()
                .Where(predicate)
                .Select(selector)
                .ToListAsync(cancellationToken);
        }

        public async Task<TProperty?> MaxOrDefaultAsync<TProperty>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, TProperty>> selector,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var query = Dbset.AsNoTracking().Where(predicate).Select(selector);
            if (!await query.AnyAsync(cancellationToken))
            {
                return default;
            }

            return await query.MaxAsync(cancellationToken);
        }
    }

    public class GenericRepository<T> : GenericRepository<T, string>, IGenericRepository<T> where T : class
    {
        public GenericRepository(AppDbContext context) : base(context) { }

        public new virtual bool Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            base.Update(entity);
            return true;
        }

        // Delete (entity) with bool result for legacy callers
        public virtual bool Delete(T entity)
        {
            if (entity == null) return false;
            base.Delete(entity);
            return true;
        }

        // Delete (id) synchronous convenience
        public virtual bool Delete(object id)
        {
            if (id == null) return false;
            var entity = Dbset.Find(id);
            if (entity == null) return false;
            Dbset.Remove(entity);
            return true;
        }

        // Delete (id) async convenience
        public virtual async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            if (id == null) return false;
            var entity = await Dbset.FindAsync(new object[] { id }, cancellationToken).AsTask();
            if (entity == null) return false;
            Dbset.Remove(entity);
            return true;
        }


        public void Add(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Dbset.Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            Dbset.AddRange(entities);
        }

        public Task<T> GetByIdAsync(object id, CancellationToken cancellationToken = default)
            => Dbset.FindAsync(new object[] { id }, cancellationToken).AsTask();

        public async Task<List<T>> GetAllAsync(
            Expression<Func<T, bool>> Filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> Order = null,
            string IncludeProperties = "",
            CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = Dbset.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(IncludeProperties))
            {
                var includes = IncludeProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(p => p.Trim());
                foreach (var include in includes)
                    query = query.Include(include);
            }

            if (Filter != null)
                query = query.Where(Filter);

            var finalQuery = Order != null ? Order(query) : query;
            return await finalQuery.ToListAsync(cancellationToken);
        }
    }
}