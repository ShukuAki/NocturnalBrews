using System;
using System.Collections.Generic;

namespace NocturnalBrews.Models;

public partial class Addon
{
    public int AddonId { get; set; }

    public string AddonName { get; set; } = null!;

    public int AddonPrice { get; set; }
}
