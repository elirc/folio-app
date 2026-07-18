using System.Net;
using System.Net.Http.Json;

namespace Folio.Tests;

public class HealthEndpointTests : IClassFixture<FolioApiFactory>
{
    private readonly FolioApiFactory _factory;

    public HealthEndpointTests(FolioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok_with_db_probe_and_timestamp()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
        Assert.Equal("up", body.Database); // structured DB probe
        Assert.NotEqual(default, body.Timestamp);
    }
}
