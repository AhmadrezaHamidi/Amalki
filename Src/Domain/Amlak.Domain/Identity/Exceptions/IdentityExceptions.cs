using Msg = Amlak.Domain.Resources.Message;

namespace Amlak.Domain.Identity.Exceptions;

public sealed class UserNotFoundException        : BusinessException { public UserNotFoundException()        : base(Msg.UserNotFoundException) { } }
public sealed class UserSuspendedException       : BusinessException { public UserSuspendedException()       : base(Msg.UserSuspendedException) { } }
public sealed class OtpNotFoundException         : BusinessException { public OtpNotFoundException()         : base(Msg.OtpNotFoundException) { } }
public sealed class OtpExpiredException          : BusinessException { public OtpExpiredException()          : base(Msg.OtpExpiredException) { } }
public sealed class OtpAlreadyUsedException      : BusinessException { public OtpAlreadyUsedException()      : base(Msg.OtpAlreadyUsedException) { } }
public sealed class OtpInvalidException          : BusinessException { public OtpInvalidException()          : base(Msg.OtpInvalidException) { } }
public sealed class OtpRequestTooSoonException   : BusinessException { public OtpRequestTooSoonException()   : base(Msg.OtpRequestTooSoonException) { } }
public sealed class InvalidRefreshTokenException : BusinessException { public InvalidRefreshTokenException() : base(Msg.InvalidRefreshTokenException) { } }
