// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Application.Queries.GetAppVersion.GetAppVersionQuery
// Assembly: RPlus.Gateway.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67A55195-718A-4D21-B898-C0A623E6660E
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Application.dll

using MediatR;
using RPlus.Gateway.Application.Contracts.Responses;

#nullable enable
namespace RPlus.Gateway.Application.Queries.GetAppVersion;

public record GetAppVersionQuery(string AppName) : IRequest<AppVersionResponse?>, IBaseRequest;
