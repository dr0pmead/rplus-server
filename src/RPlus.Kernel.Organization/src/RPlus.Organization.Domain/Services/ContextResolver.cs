// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Services.ContextResolver
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using RPlus.Organization.Domain.Entities;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

#nullable enable
namespace RPlus.Organization.Domain.Services;

public class ContextResolver
{
  public JsonDocument ResolveContexts(
    IEnumerable<NodeContext> nodeContexts,
    IEnumerable<PositionContext> positionContexts)
  {
    JsonObject target = new JsonObject();
    foreach (NodeContext nodeContext in nodeContexts)
    {
      if (nodeContext.InheritanceStrategy == "OVERRIDE")
        target = this.CloneJson(nodeContext.Data);
      else
        this.MergeJson(target, nodeContext.Data);
    }
    foreach (PositionContext positionContext in positionContexts)
    {
      if (positionContext.InheritanceStrategy == "OVERRIDE")
        target = this.CloneJson(positionContext.Data);
      else
        this.MergeJson(target, positionContext.Data);
    }
    return JsonDocument.Parse(target.ToJsonString());
  }

  private JsonObject CloneJson(JsonDocument doc)
  {
    return JsonNode.Parse(doc.RootElement.GetRawText()) is JsonObject jsonObject ? jsonObject : new JsonObject();
  }

  private void MergeJson(JsonObject target, JsonDocument sourceDoc)
  {
    if (!(JsonNode.Parse(sourceDoc.RootElement.GetRawText()) is JsonObject jsonObject))
      return;
    foreach (KeyValuePair<string, JsonNode> keyValuePair in jsonObject)
    {
      if (target.ContainsKey(keyValuePair.Key))
        target.Remove(keyValuePair.Key);
      target[keyValuePair.Key] = keyValuePair.Value?.DeepClone();
    }
  }
}
