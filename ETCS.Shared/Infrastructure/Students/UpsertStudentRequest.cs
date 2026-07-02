namespace ETCS.Shared.Infrastructure.Students;

/// <summary>
/// Payload for <c>spInsertStudentInfo</c> (insert and update). Password is plain text over HTTPS; it is hashed before the database call.
/// </summary>
public sealed class UpsertStudentRequest
{
    public string StudCode { get; set; } = string.Empty;

    public string StudUserName { get; set; } = string.Empty;

    /// <summary>Plain password; required on create. On update, omit or leave empty to leave the password unchanged in SQL (NULL passed).</summary>
    public string? StudPassword { get; set; }

    public string StudCountryID { get; set; } = string.Empty;

    public string StudSchoolID { get; set; } = string.Empty;

    public string StudStd { get; set; } = string.Empty;

    public string StudDiv { get; set; } = string.Empty;

    public string Year { get; set; } = string.Empty;

    public string StudFirstName { get; set; } = string.Empty;

    public string StudLastName { get; set; } = string.Empty;

    public string StudAdd1 { get; set; } = string.Empty;

    public string StudAdd2 { get; set; } = string.Empty;

    public string StudCity { get; set; } = string.Empty;

    public string StudState { get; set; } = string.Empty;

    public string StudCountry { get; set; } = string.Empty;

    public string StudDOB { get; set; } = string.Empty;

    public string StudGender { get; set; } = string.Empty;

    public string StudEmailId { get; set; } = string.Empty;

    public string StudSecutityQue { get; set; } = string.Empty;

    public string StudSecurityAns { get; set; } = string.Empty;

    public string StudMobile { get; set; } = string.Empty;

    public string BlackListReason { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public string IDCardStatus { get; set; } = string.Empty;

    public string SchoolCode { get; set; } = string.Empty;

    public string GuardianID { get; set; } = string.Empty;
}
