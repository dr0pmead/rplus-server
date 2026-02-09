// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Application.Commands.UpdateUserStatus.UpdateUserStatusCommandHandler
// Assembly: RPlus.Users.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 48B001A8-2E15-4980-831E-0027ECCC6407
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Application.dll

using MediatR;
using RPlus.Users.Application.Interfaces.Messaging;
using RPlus.Users.Application.Interfaces.Monitoring;
using RPlus.Users.Application.Interfaces.Repositories;
using RPlus.Users.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Users.Application.Commands.UpdateUserStatus;

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand>
{
  private readonly IUserRepository _userRepository;
  private readonly IUserEventPublisher _eventPublisher;
  private readonly IUserMetrics _metrics;
  private readonly TimeProvider _timeProvider;

  public UpdateUserStatusCommandHandler(
    IUserRepository userRepository,
    IUserEventPublisher eventPublisher,
    IUserMetrics metrics,
    TimeProvider timeProvider)
  {
    this._userRepository = userRepository;
    this._eventPublisher = eventPublisher;
    this._metrics = metrics;
    this._timeProvider = timeProvider;
  }

  public async Task Handle(UpdateUserStatusCommand request, CancellationToken ct)
  {
    UserEntity user = await this._userRepository.GetByIdAsync(request.UserId, ct);
    if (user == null)
      throw new InvalidOperationException($"User {request.UserId} not found.");
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    user.UpdateStatus(request.Status, now);
    await this._userRepository.UpdateAsync(user, ct);
    await this._eventPublisher.PublishUserStatusChangedAsync(user.Id, user.Status, now, ct);
    this._metrics.IncStatusChanged(user.Status.ToString());
    user = (UserEntity) null;
  }
}
