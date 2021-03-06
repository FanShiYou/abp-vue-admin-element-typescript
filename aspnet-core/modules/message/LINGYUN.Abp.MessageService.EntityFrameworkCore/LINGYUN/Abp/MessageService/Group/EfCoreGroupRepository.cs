using LINGYUN.Abp.MessageService.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace LINGYUN.Abp.MessageService.Group
{
    public class EfCoreGroupRepository : EfCoreRepository<IMessageServiceDbContext, ChatGroup, long>,
        IGroupRepository, ITransientDependency
    {
        public EfCoreGroupRepository(
            IDbContextProvider<IMessageServiceDbContext> dbContextProvider) 
            : base(dbContextProvider)
        {
        }

        public virtual async Task<ChatGroup> FindByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            return await (await GetDbSetAsync())
                .Where(x => x.GroupId.Equals(id))
                .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<UserGroupCard>> GetGroupAdminAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var dbContext = await GetDbContextAsync();
            var groupAdmins = await (from gp in dbContext.Set<ChatGroup>()
                                     join ucg in dbContext.Set<UserChatGroup>()
                                       on gp.GroupId equals ucg.GroupId
                                     join ugc in dbContext.Set<UserGroupCard>()
                                       on ucg.UserId equals ugc.UserId
                                     where ugc.IsAdmin
                                     select ugc)
                                     .ToListAsync(GetCancellationToken(cancellationToken));
            return groupAdmins;
        }

        public virtual async Task<bool> UserHasBlackedAsync(
            long id, 
            Guid formUserId,
            CancellationToken cancellationToken = default)
        {
            var userHasBlack = await (await GetDbContextAsync()).Set<GroupChatBlack>()
                .AnyAsync(x => x.GroupId.Equals(id) && x.ShieldUserId.Equals(formUserId), GetCancellationToken(cancellationToken));
            return userHasBlack;
        }
    }
}
