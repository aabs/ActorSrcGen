using Microsoft.CodeAnalysis;

namespace ActorSrcGen.Tests.Unit;

public class DiagnosticTests
{
    [Fact]
    public void Diagnostics_AreDefined()
    {
        Assert.Equal("ASG0001", ActorSrcGen.Diagnostics.Diagnostics.ASG0001.Id);
        Assert.Equal("ASG0002", ActorSrcGen.Diagnostics.Diagnostics.ASG0002.Id);
        Assert.Equal("ASG0003", ActorSrcGen.Diagnostics.Diagnostics.ASG0003.Id);
    }

    [Fact]
    public void Diagnostics_CreateDiagnostic_PopulatesMessage()
    {
        var diagnostic = ActorSrcGen.Diagnostics.Diagnostics.CreateDiagnostic(ActorSrcGen.Diagnostics.Diagnostics.ASG0001, Location.None, "Sample");

        Assert.Equal(ActorSrcGen.Diagnostics.Diagnostics.ASG0001.Id, diagnostic.Id);
        Assert.Contains("Sample", diagnostic.GetMessage());
    }
}
