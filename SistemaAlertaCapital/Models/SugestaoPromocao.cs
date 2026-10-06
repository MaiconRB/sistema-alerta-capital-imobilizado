namespace SistemaAlertaCapital.Models;

/// <summary>
/// Sugestão de promoção para um produto parado, exibida no modal do painel.
/// Os valores numéricos são SEMPRE calculados pelo sistema (RegrasPromocao),
/// mesmo quando a sugestão vem da IA, garantindo que as regras da loja sejam respeitadas.
/// </summary>
public record SugestaoPromocao
{
    /// <summary>Tipo de ação sugerida (ex.: "Desconto direto", "Leve 3 pague 2").</summary>
    public string TipoPromocao { get; init; } = string.Empty;

    /// <summary>Preço de venda atual, antes da promoção.</summary>
    public decimal PrecoOriginal { get; init; }

    /// <summary>Preço unitário efetivo ao cliente durante a promoção.</summary>
    public decimal PrecoPromocional { get; init; }

    /// <summary>Desconto em relação ao preço original, em % (ex.: 20,5).</summary>
    public decimal DescontoPercentual { get; init; }

    /// <summary>Lucro por unidade vendida na promoção (preço promocional − custo).</summary>
    public decimal LucroUnitario { get; init; }

    /// <summary>Lucro sobre o custo, em % (ex.: 15 significa 15% acima do custo).</summary>
    public decimal MargemSobreCustoPercentual { get; init; }

    /// <summary>Valor que entra no caixa se todo o estoque for vendido na promoção.</summary>
    public decimal ValorRecuperavel { get; init; }

    /// <summary>Explicação de por que a promoção faz sentido para este produto.</summary>
    public string Justificativa { get; init; } = string.Empty;

    /// <summary>Mensagem pronta para divulgar (WhatsApp / redes sociais).</summary>
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>true se a sugestão veio da IA; false se é a sugestão padrão da loja.</summary>
    public bool GeradaPorIA { get; init; }

    /// <summary>
    /// true se a IA sugeriu um preço abaixo do custo e o sistema o elevou até o
    /// preço mínimo permitido (regra: nunca vender abaixo do custo).
    /// </summary>
    public bool PrecoAjustadoPeloPiso { get; init; }
}
