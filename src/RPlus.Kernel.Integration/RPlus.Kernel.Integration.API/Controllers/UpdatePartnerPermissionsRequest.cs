// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.UpdatePartnerPermissionsRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

public record UpdatePartnerPermissionsRequest(List<string> PermissionIds);
