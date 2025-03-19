using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class InventoryTb
{
    public int InvId { get; set; }

    public string? Ingredient { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? Remaining { get; set; }

    public string? Measurement { get; set; }

    public DateTime? Timestamp { get; set; }
}
