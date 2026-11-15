using Bunit;
using Frontend.Components;
using Frontend.Models;
using Xunit;

namespace Frontend.Tests;

public class ProblemDetailsViewTests : BunitContext
{
    [Fact]
    public void ShouldDisplayProblemDetailsCorrectly()
    {
        // Arrange
        var problem = new ApiProblemDetails
        {
            Title = "Account is blocked",
            Status = 422,
            Code = "ACCOUNT_BLOCKED",
            Detail = "This account does not allow posting."
        };

        // Act
        var cut = Render<ProblemDetailsView>(parameters =>
            parameters.Add(p => p.Problem, problem)
        );

        // Assert
        Assert.Contains("Account is blocked", cut.Markup);
        Assert.Contains("422", cut.Markup);
        Assert.Contains("ACCOUNT_BLOCKED", cut.Markup);
        Assert.Contains("This account does not allow posting.", cut.Markup);
    }
}