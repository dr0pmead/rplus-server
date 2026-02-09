// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Common.Behaviors.ValidationBehavior`2
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using FluentValidation;
using FluentValidation.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
  private readonly IEnumerable<IValidator<TRequest>> _validators;

  public ValidationBehavior(IEnumerable<IValidator<TRequest>> _validators)
  {
    this._validators = _validators;
  }

  public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
  {
    if (this._validators.Any<IValidator<TRequest>>())
    {
      ValidationContext<TRequest> context = new ValidationContext<TRequest>(request);
      List<ValidationFailure> list = ((IEnumerable<ValidationResult>) await Task.WhenAll<ValidationResult>(this._validators.Select<IValidator<TRequest>, Task<ValidationResult>>((Func<IValidator<TRequest>, Task<ValidationResult>>) (v => v.ValidateAsync((IValidationContext) context, cancellationToken))))).SelectMany<ValidationResult, ValidationFailure>((Func<ValidationResult, IEnumerable<ValidationFailure>>) (r => (IEnumerable<ValidationFailure>) r.Errors)).Where<ValidationFailure>((Func<ValidationFailure, bool>) (f => f != null)).ToList<ValidationFailure>();
      if (list.Count != 0)
        throw new ValidationException((IEnumerable<ValidationFailure>) list);
    }
    return await next();
  }
}
