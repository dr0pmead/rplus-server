// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.Events.NodeMovedEvent
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using System;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging.Events;

public record NodeMovedEvent(Guid NodeId, string OldPath, string NewPath);
