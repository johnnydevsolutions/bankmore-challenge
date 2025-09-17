namespace ContaCorrente.Domain.Entities;

public class ContaCorrente
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public long Numero { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public string SenhaHash { get; set; } = string.Empty;
    public string SenhaSalt { get; set; } = string.Empty;
    public string CpfHash { get; set; } = string.Empty;
    public string CpfSalt { get; set; } = string.Empty;
    public string CpfIndex { get; set; } = string.Empty;
}
