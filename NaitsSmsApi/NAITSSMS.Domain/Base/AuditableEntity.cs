using System;
using System.ComponentModel.DataAnnotations;

namespace NAITSSMS.Domain.Base
{

    public abstract class AuditableEntity<TPrimaryKey> : Entity<TPrimaryKey>, IAuditableEntity
    {
        [ScaffoldColumn(false)]
        public DateTime CreatedDate { get; set; }

        [MaxLength(70)]
        [ScaffoldColumn(false)]
        public string CreatedBy { get; set; }

        [ScaffoldColumn(false)]
        public DateTime UpdatedDate { get; set; }

        [MaxLength(70)]
        [ScaffoldColumn(false)]
        public string UpdatedBy { get; set; }
    }

    public abstract class AuditableEntity:AuditableEntity<string>{}
}
