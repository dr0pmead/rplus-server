// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.LoginWithPassword.LoginWithPasswordCommandValidator
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using FluentValidation;
using RPlus.SDK.Auth.Commands;
using System;
using System.Linq.Expressions;

#nullable enable
namespace RPlus.Auth.Application.Commands.LoginWithPassword;

public class LoginWithPasswordCommandValidator : AbstractValidator<LoginWithPasswordCommand>
{
  public LoginWithPasswordCommandValidator()
  {
    this.RuleFor<string>((Expression<Func<LoginWithPasswordCommand, string>>) (x => x.Phone)).NotEmpty<LoginWithPasswordCommand, string>().Matches<LoginWithPasswordCommand>("^\\+?[1-9]\\d{1,14}$").WithMessage<LoginWithPasswordCommand, string>("Invalid phone number format (E.164 expected)");
    this.RuleFor<string>((Expression<Func<LoginWithPasswordCommand, string>>) (x => x.Password)).NotEmpty<LoginWithPasswordCommand, string>().MinimumLength<LoginWithPasswordCommand>(8).WithMessage<LoginWithPasswordCommand, string>("Password must be at least 8 characters");
    this.RuleFor<string>((Expression<Func<LoginWithPasswordCommand, string>>) (x => x.DeviceId)).NotEmpty<LoginWithPasswordCommand, string>().WithMessage<LoginWithPasswordCommand, string>("DeviceId is required");
  }
}
