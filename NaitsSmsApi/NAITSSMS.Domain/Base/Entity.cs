using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NAITSSMS.Domain.Base
{
    public interface IBaseEntity<TPrimaryKey>
    {
       TPrimaryKey Id { get; set; }
    }
    public abstract class BaseEntity<TPrimaryKey>:IBaseEntity<TPrimaryKey>
    {
        public virtual TPrimaryKey Id { get; set; }
    }

    public abstract class Entity<TPrimaryKey> : BaseEntity<TPrimaryKey>,
        IEntity<TPrimaryKey>
    {

    }
    public abstract class Entity : Entity<string>
    {
    }
}
