namespace ETCS.Web.Infrastructure.Navigation;

public interface IParentPortalNavigationService
{
    Task<ParentPortalNavigationAccess> GetAccessAsync(int guardianId, CancellationToken cancellationToken = default);
}
