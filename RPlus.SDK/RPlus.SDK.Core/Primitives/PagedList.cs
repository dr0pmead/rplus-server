// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Primitives.PagedList`1
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace RPlus.SDK.Core.Primitives;

public class PagedList<T>
{
  public IReadOnlyCollection<T> Items { get; }

  public int PageNumber { get; }

  public int PageSize { get; }

  public int TotalCount { get; }

  public int TotalPages { get; }

  public bool HasNextPage => this.PageNumber < this.TotalPages;

  public bool HasPreviousPage => this.PageNumber > 1;

  public PagedList(IEnumerable<T> items, int count, int pageNumber, int pageSize)
  {
    this.PageNumber = pageNumber;
    this.PageSize = pageSize;
    this.TotalCount = count;
    this.TotalPages = (int) Math.Ceiling((double) count / (double) pageSize);
    this.Items = (IReadOnlyCollection<T>) items.ToList<T>().AsReadOnly();
  }

  public static PagedList<T> Create(IQueryable<T> source, int pageNumber, int pageSize)
  {
    int count = source.Count<T>();
    return new PagedList<T>((IEnumerable<T>) source.Skip<T>((pageNumber - 1) * pageSize).Take<T>(pageSize).ToList<T>(), count, pageNumber, pageSize);
  }
}
