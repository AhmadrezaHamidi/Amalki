using Msg = Amlak.Domain.Resources.Message;

namespace Amlak.Domain.Lead.Exceptions;

public sealed class LeadNotFoundException          : BusinessException { public LeadNotFoundException()          : base(Msg.LeadNotFoundException) { } }
public sealed class InvalidLeadTransitionException : BusinessException { public InvalidLeadTransitionException() : base(Msg.InvalidLeadTransitionException) { } }
public sealed class TooManyLeadsException          : BusinessException { public TooManyLeadsException()          : base(Msg.TooManyLeadsException) { } }
