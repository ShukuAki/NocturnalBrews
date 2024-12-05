using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class CupsListTb
{
    public int CupId { get; set; }

    public string? Size { get; set; }

    public int? Quantity { get; set; }

    public int? PricePerCup { get; set; }

    public int? PriceWholeSale { get; set; }
}
