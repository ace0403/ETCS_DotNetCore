using System;
using System.Collections.Generic;
using System.Text;

namespace ETCS.Shared.Infrastructure.Auth.Models
{
    public sealed record UserResponse(int id, string name, string? email);
}
