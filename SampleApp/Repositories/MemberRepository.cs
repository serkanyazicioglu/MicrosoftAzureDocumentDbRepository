using Nhea.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SampleApp.Repositories
{
    public partial class Member : Microsoft.Azure.Documents.Resource
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public int Status { get; set; }

        public string Email { get; set; }
    }

    public class MemberRepository : BaseDocDbRepository<Member>
    {
        public override Member CreateNew()
        {
            var entity = base.CreateNew();
            entity.Id = Guid.NewGuid();
            entity.Status = (int)StatusType.Available;

            return entity;
        }

        //public override Expression<Func<Member, object>> DefaultSorter => query => new { query.Timestamp };

        //protected override SortDirection DefaultSortType => SortDirection.Descending;

        public override Expression<Func<Member, bool>> DefaultFilter => query => query.Status == (int)StatusType.Available;
    }
}
