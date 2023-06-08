using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BunkerBot;

public class Profession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Luggage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public virtual List<BUser>? Users { get; set; } = new();
}

public class HealthCondition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Biology
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdditionalInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Hobby
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SpecialCard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public virtual List<BUser>? Users { get; set; } = new();
}

public class BunkerInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public virtual List<BGame>? Games { get; set; } = new();
    public virtual List<BGame>? GamesExile { get; set; } = new();
}

public class Catastrophe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Hazard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public virtual List<BGame>? Games { get; set; } = new();
}

