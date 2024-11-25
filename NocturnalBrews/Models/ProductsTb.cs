using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class ProductsTb
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Categories { get; set; }

    public int? Small { get; set; }

    public int? Medium { get; set; }

    public int? Large { get; set; }
}
