// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Application.DTOs.CreateOrganizationDto
// Assembly: RPlus.Organization.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 89C3F9D0-9320-401B-87D7-D65FA90FB0E0
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Application.dll

using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace RPlus.Organization.Application.DTOs;

public record CreateOrganizationDto(
  Guid? ParentId,
  string Name,
  string Description,
  List<Guid> Leaders,
  List<Guid> Deputies,
  List<Guid> Members,
  JsonDocument? Metadata,
  JsonDocument? Rules)
;
