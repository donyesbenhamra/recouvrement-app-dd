using Xunit;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using RecouvrementAPI;

namespace RecouvrementAPI.Tests
{
    public class RecouvrementTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public RecouvrementTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetDossiers_ReturnsSuccess()
        {
            var response = await _client.GetAsync("/api/dossierrecouvrement");
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task GetScore_WithValidId_ReturnsOkOrNotFound()
        {
            var response = await _client.GetAsync("/api/scoringrisque/1");
            Assert.True(
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.NotFound
            );
        }

        [Fact]
        public async Task GetClients_ReturnsSuccess()
        {
            var response = await _client.GetAsync("/api/client");
            Assert.True(response.IsSuccessStatusCode);
        }
    }
}