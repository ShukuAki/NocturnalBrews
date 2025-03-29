using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class InventoryTb
{
    public int InventoryId { get; set; }

    public decimal Stock { get; set; }

    public decimal Used { get; set; }

    public string Measurement { get; set; } = null!;

    public DateTime DateToday { get; set; }

    public decimal InventoryIncoming { get; set; }

    public string Name { get; set; } = null!;
}
