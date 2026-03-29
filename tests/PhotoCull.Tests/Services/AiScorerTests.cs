using PhotoCull.Services;
using Xunit;

namespace PhotoCull.Tests.Services;

public class AiScorerTests
{
    [Fact]
    public void ScoreWithInvalidDataReturnsDefaults()
    {
        var scorer = new AiScorer();
        var score = scorer.Score(Array.Empty<byte>());
        Assert.Equal(50, score.Overall);
        Assert.Equal(50, score.Sharpness);
    }

    [Fact]
    public void ScoreRangeValid()
    {
        var scorer = new AiScorer();
        // Create a simple white JPEG-like image for testing
        // In real tests this would be an actual image
        var score = scorer.Score(Array.Empty<byte>());
        Assert.InRange(score.Overall, 0, 100);
        Assert.InRange(score.Sharpness, 0, 100);
        Assert.InRange(score.Exposure, 0, 100);
    }
}
