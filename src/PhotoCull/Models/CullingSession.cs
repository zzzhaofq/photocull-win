namespace PhotoCull.Models;

public class CullingSession
{
    public Guid Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int RejectedCount { get; set; }
    public int SelectedCount { get; set; }
    public Round CurrentRound { get; set; } = Round.QuickCull;
    public int CurrentPhotoIndex { get; set; }
    public int CurrentGroupIndex { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Photo> Photos { get; set; } = new();
    public List<PhotoGroup> Groups { get; set; } = new();

    public CullingSession()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public CullingSession(string folderPath) : this()
    {
        FolderPath = folderPath;
    }
}
