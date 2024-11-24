using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class ProductsTb
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string Size { get; set; } = null!;

    public string Price { get; set; } = null!;

    public virtual ICollection<OrdersTb> OrdersTbs { get; set; } = new List<OrdersTb>();
}
