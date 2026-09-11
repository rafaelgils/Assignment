using Order.Application.Common;

namespace Order.Application.Tests;

public class PagedResultTests
{
    [Fact]
    public void Defaults_to_an_empty_page()
    {
        var result = new PagedResult<string>();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Page);
        Assert.Equal(0, result.PageSize);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void Stores_the_provided_values()
    {
        var result = new PagedResult<string>
        {
            Items = ["a", "b"],
            Page = 2,
            PageSize = 5,
            TotalCount = 12
        };

        Assert.Equal(["a", "b"], result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(12, result.TotalCount);
    }
}
