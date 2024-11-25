using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class OrdersTb
{
    public int OrderId { get; set; }

    public int Price { get; set; }

    public string Mop { get; set; } = null!;

    public int? Change { get; set; }

    public string? ProductsArray { get; set; }

    public decimal? Total { get; set; }

    public DateTime? OrderDateTime { get; set; }

    public string Status { get; set; } = null!;
}
