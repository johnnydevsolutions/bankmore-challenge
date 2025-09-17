using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public class ContaIntegrationTests : IClassFixture<WebApplicationFactory<ContaCorrente.Api.ApiMarker>>
{
    private readonly WebApplicationFactory<ContaCorrente.Api.ApiMarker> _factory;
    public ContaIntegrationTests(WebApplicationFactory<ContaCorrente.Api.ApiMarker> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "conta_api_tests");
            System.IO.Directory.CreateDirectory(temp);
            var db = System.IO.Path.Combine(temp, "conta.db");
            b.UseSetting("ConnectionStrings:Default", $"Data Source={db}");
        });
    }

    [Fact]
    public async Task Cadastro_Login_Saldo_FluxoBasico()
    {
        var client = _factory.CreateClient();
        var cadastro = JsonSerializer.Serialize(new { cpf = "52998224725", nome = "Ana", senha = "senha123" });
        var respC = await client.PostAsync("/contas/cadastrar", new StringContent(cadastro, Encoding.UTF8, "application/json"));
        respC.EnsureSuccessStatusCode();
        var bodyC = await respC.Content.ReadAsStringAsync();
        bodyC.Should().Contain("numero");

        var numero = JsonDocument.Parse(bodyC).RootElement.GetProperty("numero").GetInt64();

        var login = JsonSerializer.Serialize(new { documentoOuNumero = numero.ToString(), senha = "senha123" });
        var respL = await client.PostAsync("/contas/login", new StringContent(login, Encoding.UTF8, "application/json"));
        respL.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await respL.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var mov = JsonSerializer.Serialize(new { idempotencia = "t1", valor = 100.0m, tipo = 'C' });
        var respM = await client.PostAsync("/contas/movimentar", new StringContent(mov, Encoding.UTF8, "application/json"));
        respM.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var respS = await client.GetAsync("/contas/saldo");
        respS.EnsureSuccessStatusCode();
        var saldo = JsonDocument.Parse(await respS.Content.ReadAsStringAsync()).RootElement.GetProperty("saldo").GetDecimal();
        saldo.Should().Be(100.0m);
    }
}
