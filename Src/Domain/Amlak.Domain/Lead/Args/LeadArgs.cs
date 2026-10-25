namespace Amlak.Domain.Lead.Args;

public sealed record SubmitLeadArg(long Id, long? PropertyId, string FullName, string PhoneNumber, string? Message);
