namespace Gateway.API.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Limite global, aplicado a todo request, particionado por IP do cliente.
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 10;
    public int QueueLimit { get; set; } = 0;

    // Limite mais restrito, só para /auth/login (alvo comum de brute-force/DDoS).
    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
}
