using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class InventoryTb
{
    public int InventoryId { get; set; }

    public string? Ingredient { get; set; }

    public int? Quantity { get; set; }

    public double? PricePer { get; set; }

    public double? PriceWholeSale { get; set; }
}
