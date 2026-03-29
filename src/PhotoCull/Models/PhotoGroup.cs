using System.Text.Json;

namespace PhotoCull.Models;

public class PhotoGroup
{
    public Guid Id { get; set; }
    public GroupType GroupType { get; set; }
    public bool IsReviewed { get; set; }
    public Guid? RecommendedPhotoId { get; set; }

    // Stored as JSON string in DB
    public string SelectedPhotoIdsJson { get; set; } = "[]";

    public List<Guid> SelectedPhotoIds
    {
        get => JsonSerializer.Deserialize<List<Guid>>(SelectedPhotoIdsJson) ?? new();
        set => SelectedPhotoIdsJson = JsonSerializer.Serialize(value);
    }

    public Guid? SessionId { get; set; }
    public CullingSession? Session { get; set; }

    public List<Photo> Photos { get; set; } = new();

    public PhotoGroup()
    {
        Id = Guid.NewGuid();
    }

    public PhotoGroup(GroupType groupType) : this()
    {
        GroupType = groupType;
    }
}
