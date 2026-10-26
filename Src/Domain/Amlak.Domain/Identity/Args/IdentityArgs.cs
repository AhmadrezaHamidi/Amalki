using Amlak.Domain.Identity.Enums;

namespace Amlak.Domain.Identity.Args;

public sealed record CreateUserArg(long Id, string PhoneNumber, UserRole Role = UserRole.Customer);
