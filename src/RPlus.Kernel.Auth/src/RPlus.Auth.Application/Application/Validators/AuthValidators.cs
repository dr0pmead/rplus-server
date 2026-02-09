// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Validators.RequestOtpCommandValidator
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using FluentValidation;
using RPlus.SDK.Auth.Commands;
using System;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Auth.Application.Validators;

public class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
  public RequestOtpCommandValidator()
  {
    this.RuleFor<string>((Expression<Func<RequestOtpCommand, string>>) (x => x.Phone)).NotEmpty<RequestOtpCommand, string>().WithMessage<RequestOtpCommand, string>("Phone is required").Matches<RequestOtpCommand>("^\\+?[1-9]\\d{1,14}$").WithMessage<RequestOtpCommand, string>("Invalid phone format (E.164 expected)");
    this.RuleFor<string>((Expression<Func<RequestOtpCommand, string>>) (x => x.DeviceId)).NotEmpty<RequestOtpCommand, string>().WithMessage<RequestOtpCommand, string>("DeviceId is required").MaximumLength<RequestOtpCommand>(128 /*0x80*/).WithMessage<RequestOtpCommand, string>("DeviceId too long");
  }
}
