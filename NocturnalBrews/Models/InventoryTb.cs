using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class InventoryTb
{
    public int InventoryId { get; set; }

    public decimal? Stock { get; set; }

    public decimal? Used { get; set; }

    public string? Measurement { get; set; }

    public DateTime? Timestamp { get; set; }

    public string? Name { get; set; }
}
