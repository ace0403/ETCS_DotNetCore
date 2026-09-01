using System.ComponentModel.DataAnnotations;
using ETCS.Shared.Infrastructure.Admin.Auth;

namespace ETCS.Shared.Infrastructure.Admin.Models.Requests;

public sealed class AdminSelectRoleRequest
{
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a role.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a role.")]
    public int RoleId { get; set; }

    public IReadOnlyList<LoginAccountRoleOptionDto> Roles { get; set; } = [];
}
