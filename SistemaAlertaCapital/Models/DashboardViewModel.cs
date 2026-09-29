namespace SistemaAlertaCapital.Models;

/// <summary>
/// Reúne todas as informações exibidas no painel de Capital Imobilizado.
/// É montado pelo CapitalImobilizadoService e entregue à View pela Controller.
/// </summary>
public class DashboardViewModel
{
    /// <summary>
    /// Produtos parados há mais dias que o limite, ordenados do maior para o
    /// menor valor imobilizado (os mais críticos aparecem primeiro).
    /// </summary>
    public List<Produto> ProdutosParados { get; set; } = new();

    /// <summary>Soma do valor imobilizado de todos os produtos parados, em R$.</summary>
    public decimal TotalImobilizado { get; set; }

    /// <summary>Quantidade de produtos considerados parados.</summary>
    public int QuantidadeParados { get; set; }

    /// <summary>Quantidade de produtos com venda recente (dentro do limite).</summary>
    public int QuantidadeAtivos { get; set; }

    /// <summary>Limite de dias sem venda usado na classificação (ex.: 180).</summary>
    public int DiasLimite { get; set; }
}
