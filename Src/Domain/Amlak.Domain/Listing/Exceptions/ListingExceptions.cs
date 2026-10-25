using Msg = Amlak.Domain.Resources.Message;

namespace Amlak.Domain.Listing.Exceptions;

public sealed class PropertyNotFoundException         : BusinessException { public PropertyNotFoundException()         : base(Msg.PropertyNotFoundException) { } }
public sealed class PropertyAlreadyPublishedException : BusinessException { public PropertyAlreadyPublishedException() : base(Msg.PropertyAlreadyPublishedException) { } }
public sealed class PropertyNotPublishedException     : BusinessException { public PropertyNotPublishedException()     : base(Msg.PropertyNotPublishedException) { } }
public sealed class PropertyAlreadyClosedException    : BusinessException { public PropertyAlreadyClosedException()    : base(Msg.PropertyAlreadyClosedException) { } }
public sealed class SalePriceRequiredException        : BusinessException { public SalePriceRequiredException()        : base(Msg.SalePriceRequiredException) { } }
public sealed class RentAmountRequiredException       : BusinessException { public RentAmountRequiredException()       : base(Msg.RentAmountRequiredException) { } }
public sealed class InvalidAmountException            : BusinessException { public InvalidAmountException()            : base(Msg.InvalidAmountException) { } }
public sealed class InvalidAreaException              : BusinessException { public InvalidAreaException()              : base(Msg.InvalidAreaException) { } }
public sealed class InvalidLocationException          : BusinessException { public InvalidLocationException()          : base(Msg.InvalidLocationException) { } }
public sealed class InvalidSlugException              : BusinessException { public InvalidSlugException()              : base(Msg.InvalidSlugException) { } }
public sealed class PhotoLimitExceededException       : BusinessException { public PhotoLimitExceededException()       : base(Msg.PhotoLimitExceededException) { } }
public sealed class PhotoNotFoundException            : BusinessException { public PhotoNotFoundException()            : base(Msg.PhotoNotFoundException) { } }
public sealed class InvalidPhotoOrderException        : BusinessException { public InvalidPhotoOrderException()        : base(Msg.InvalidPhotoOrderException) { } }
public sealed class InvalidPhotoFileException         : BusinessException { public InvalidPhotoFileException()         : base(Msg.InvalidPhotoFileException) { } }
public sealed class PhotoTooLargeException            : BusinessException { public PhotoTooLargeException()            : base(Msg.PhotoTooLargeException) { } }
