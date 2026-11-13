namespace Ledger.Tests;

public class SampleTests
{
    [Fact]
    public void Adding_two_numbers_returns_their_sum()
    {
        var result = 2 + 3;

        Assert.Equal(5, result);
    }
}