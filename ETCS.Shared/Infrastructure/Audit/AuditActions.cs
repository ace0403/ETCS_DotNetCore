namespace ETCS.Shared.Infrastructure.Audit;

public static class AuditSourceApps
{
    public const string Admin = "Admin";
    public const string Web = "Web";
    public const string Api = "Api";
    public const string Pos = "Pos";
    public const string System = "System";
}

public static class AuditActorTypes
{
    public const string LoginAccount = "LoginAccount";
    public const string Guardian = "Guardian";
    public const string System = "System";
    public const string Anonymous = "Anonymous";
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string AccessDenied = "AccessDenied";
    public const string OrderInitiate = "OrderInitiate";
    public const string OrderComplete = "OrderComplete";
    public const string TopupInitiate = "TopupInitiate";
    public const string TopupComplete = "TopupComplete";
    public const string ManualTopup = "ManualTopup";
    public const string EmailSend = "EmailSend";
    public const string PaymentReconcile = "PaymentReconcile";
}

public static class AuditOutcomes
{
    public const string Success = "Success";
    public const string Failure = "Failure";
}
