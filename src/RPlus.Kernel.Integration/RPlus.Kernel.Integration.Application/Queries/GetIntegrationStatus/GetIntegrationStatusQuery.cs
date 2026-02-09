// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Queries.GetIntegrationStatus.GetIntegrationStatusQuery
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using MediatR;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Queries.GetIntegrationStatus;

public record GetIntegrationStatusQuery() : IRequest<IntegrationStatusResponse>, IBaseRequest;
