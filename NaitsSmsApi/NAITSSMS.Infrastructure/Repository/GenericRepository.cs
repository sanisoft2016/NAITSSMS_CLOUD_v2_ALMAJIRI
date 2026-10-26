using CustRewardMgtSys.Domain.IRepository;
using CustRewardMgtSys.Infrastructure.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NAITSSMS.Infrastructure.Repository
{
    public class GenericRepository<T> : IGenericRepository<T>, IDisposable where T : class
    {
        protected readonly AppDbContext context;

        public GenericRepository(AppDbContext context)
        {
            this.context = context;
        }
        public void Add(T entity)
        {
            context.Set<T>().Add(entity);
        }
        public virtual bool Update(T Entity)
        {
            try
            {
                if (Entity == null)
                {
                    return false;
                }
                context.Entry(Entity).State = EntityState.Detached;
                context.Set<T>().Attach(Entity);
                context.Entry(Entity).State = EntityState.Modified;

                return true;
            }
            catch (Exception ex)
            {
                throw ex; ;
            }
        }

        public void AddRange(IEnumerable<T> entities)
        {
            context.Set<T>().AddRange(entities);
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> expression)
        {
            return context.Set<T>().Where(expression);
        }

        public IEnumerable<T> GetAll(Expression<Func<T, bool>> Filter = null,
                                               Func<IQueryable<T>, IOrderedQueryable<T>> Order = null,
                                               string IncludeProperties = "")
        {
            try
            {
                IQueryable<T> Query = context.Set<T>();
                if (Filter != null)
                {
                    Query = Query.Where(Filter);
                }

                foreach (var includeProperty in IncludeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Query = Query.Include(includeProperty);
                }

                if (Order != null)
                {
                    return Order(Query).AsNoTracking().ToList();
                }
                else
                {
                    var GetRecord = Query.AsNoTracking().ToList();
                    return GetRecord;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public T GetById(object id)
        {
            return context.Set<T>().Find(id);
        }
        public void DeleteByObject(T entity)
        {
            context.Set<T>().Remove(entity);
        }
        public void DeleteByObjectId(object entity)
        {
            T entityObj = context.Set<T>().Find(entity);
            context.Set<T>().Remove(entityObj);
        }
        public void RemoveRange(IEnumerable<T> entities)
        {
            context.Set<T>().RemoveRange(entities);
        }
        public async Task SaveAsync()
        {
            await context.SaveChangesAsync();
        }
        public void Dispose()
        {
            context?.Dispose();
        }
    }

}