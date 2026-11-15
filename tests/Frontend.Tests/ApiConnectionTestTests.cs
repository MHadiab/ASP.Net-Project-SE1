using Bunit;
using Frontend.Components;
using System.Net;
using Microsoft.Extensions.DependencyInjection;


namespace Frontend.Tests;

public class ApiConnectionTestTests : BunitContext
{
    [Fact]
    public void ShouldDisplaySuccessWhenBackendIsAvailable()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK
        );

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8082/")
        };

        Services.AddSingleton(httpClient);

        // Act
        var cut = Render<ApiConnectionTest>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Backend is available", cut.Markup);
            Assert.Contains("200", cut.Markup);
        });
    }
}