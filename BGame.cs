namespace BunkerBot;

public class BGame
{
    public int Id { get; set; }
    public virtual List<BUser>? Users { get; set; } = new();
    public virtual List<BUser>? MaxVotesUsers { get; set; } = new();
    public virtual List<BUser>? VotingUsers { get; set; } = new();
    public virtual List<BunkerInfo>? BunkerInfos { get; set; } = new();
    public virtual List<BunkerInfo>? ExileBunkerInfos { get; set; } = new();
    public virtual List<Hazard>? Hazards { get; set; } = new();
    public virtual Catastrophe? Catastrophe { get; set; }
    public long GroupId { get; set; }
    public virtual int AdminId { get; set; } = -1;
    public virtual int StartGameBotMessageId { get; set; } = 0;
    public bool IsPaused { get; set; } = false;
    public virtual VotingList? VotingList { get; set; }
    public int Status { get; set; } = 0;
    public int RoundPart { get; set; } = 0;
    public long SpeakerId { get; set; } = 0;
    public long CurrentHazzardTargetId { get; set; } = 0;
}

public class VotingList
{
    public int Id { get; set; }
    public string roundVotings { get; set; } = String.Empty;
}
