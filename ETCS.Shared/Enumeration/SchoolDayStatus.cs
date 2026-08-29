namespace ETCS.Shared.Enumeration;

/// <summary>
/// School calendar day status. Holiday blocks meal ordering; HalfDay is display-only in v1.
/// </summary>
public enum SchoolDayStatus : byte
{
    Holiday = 0,
    FullDay = 1,
    HalfDay = 2
}
