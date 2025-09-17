namespace ContaCorrente.Domain.Entities;

public class Movimento
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ContaId { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.UtcNow;
    public char Tipo { get; set; } // C ou D
    public decimal Valor { get; set; }
}

