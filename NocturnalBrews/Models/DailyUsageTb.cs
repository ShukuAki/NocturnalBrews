using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class DailyUsageTb
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly Date { get; set; }

    public decimal Used { get; set; }
}
