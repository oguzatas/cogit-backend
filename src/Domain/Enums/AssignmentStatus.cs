namespace backend.Domain.Enums;

public enum AssignmentStatus
{
    Pending               = 0,
    InProgress            = 1,
    Completed             = 2,
    /// <summary>Set when the test contains TextInput questions that require a human reviewer.</summary>
    AwaitingManualGrading = 3
}
