using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class OrdersTb
{
    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Price { get; set; }

    public string Mop { get; set; } = null!;

    public int? Change { get; set; }

    public virtual ProductsTb Product { get; set; } = null!;
}
