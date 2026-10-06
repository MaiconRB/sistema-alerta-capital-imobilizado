using System.Globalization;
using SistemaAlertaCapital.Models;

namespace SistemaAlertaCapital.Services;

/// <summary>
/// Regras de negócio das promoções. Não depende de IA: é o "guarda" que
/// valida qualquer sugestão antes de ela chegar à tela.
///
/// Regras:
/// 1. Piso de preço: o preço promocional NUNCA fica abaixo do custo unitário.
/// 2. Desconto padrão por faixa de dias sem venda (usado quando a IA está
///    indisponível e também como referência enviada à IA):
///    - até 270 dias ........ DescontoAte270Dias   (padrão 10%)
///    - de 271 a 365 dias ... DescontoAte365Dias   (padrão 20%)
///    - acima de 365 dias ... DescontoAcima365Dias (padrão 30%)
/// 3. Todos os indicadores (desconto, lucro, valor recuperável) são
///    recalculados aqui, e não copiados da resposta da IA.
/// </summary>
public class RegrasPromocao
{
    // Percentuais de desconto por faixa, lidos da seção "Promocao" do appsettings.json
    private readonly decimal _descontoAte270;
    private readonly decimal _descontoAte365;
    private readonly decimal _descontoAcima365;

    // Cultura brasileira para formatar valores em R$ nas mensagens
    private static readonly CultureInfo CulturaBr = new("pt-BR");

    public RegrasPromocao(IConfiguration configuracao)
    {
        _descontoAte270 = configuracao.GetValue("Promocao:DescontoAte270Dias", 10m);
        _descontoAte365 = configuracao.GetValue("Promocao:DescontoAte365Dias", 20m);
        _descontoAcima365 = configuracao.GetValue("Promocao:DescontoAcima365Dias", 30m);
    }

    /// <summary>
    /// Desconto de referência (em %) conforme há quanto tempo o produto está parado.
    /// </summary>
    public decimal DescontoPadraoPercentual(int diasSemVenda) => diasSemVenda switch
    {
        <= 270 => _descontoAte270,
        <= 365 => _descontoAte365,
        _ => _descontoAcima365
    };

    /// <summary>
    /// Menor preço promocional permitido: o custo unitário (regra "nunca abaixo do custo").
    /// </summary>
    public decimal PrecoMinimo(Produto produto) => produto.CustoUnitario;

    /// <summary>
    /// Aplica o piso ao preço sugerido. Retorna o preço final e se houve ajuste.
    /// </summary>
    public (decimal Preco, bool Ajustado) AplicarPiso(Produto produto, decimal precoSugerido)
    {
        var preco = Math.Round(precoSugerido, 2);
        var minimo = PrecoMinimo(produto);

        return preco < minimo ? (minimo, true) : (preco, false);
    }

    /// <summary>
    /// Monta a sugestão final, recalculando todos os indicadores a partir do preço.
    /// </summary>
    public SugestaoPromocao Montar(
        Produto produto, string tipo, decimal precoPromocional,
        string justificativa, string mensagem, bool geradaPorIA, bool precoAjustado)
    {
        var lucroUnitario = precoPromocional - produto.CustoUnitario;

        return new SugestaoPromocao
        {
            TipoPromocao = tipo,
            PrecoOriginal = produto.PrecoVenda,
            PrecoPromocional = precoPromocional,
            // Desconto = quanto o preço caiu em relação ao preço original
            DescontoPercentual = Math.Round((1 - precoPromocional / produto.PrecoVenda) * 100, 1),
            LucroUnitario = lucroUnitario,
            // Margem = quanto o preço promocional está acima do custo
            MargemSobreCustoPercentual = Math.Round(lucroUnitario / produto.CustoUnitario * 100, 1),
            // Valor que volta para o caixa se todo o estoque for vendido
            ValorRecuperavel = precoPromocional * produto.QuantidadeEstoque,
            Justificativa = justificativa,
            Mensagem = mensagem,
            GeradaPorIA = geradaPorIA,
            PrecoAjustadoPeloPiso = precoAjustado
        };
    }

    /// <summary>
    /// Sugestão padrão (sem IA): desconto direto conforme a faixa de dias parados,
    /// respeitando o piso de preço.
    /// </summary>
    public SugestaoPromocao SugestaoPadrao(Produto produto)
    {
        var desconto = DescontoPadraoPercentual(produto.DiasSemVenda);
        var (preco, ajustado) = AplicarPiso(produto, produto.PrecoVenda * (1 - desconto / 100));

        var justificativa = ajustado
            ? $"Produto parado há {produto.DiasSemVenda} dias. O desconto padrão da faixa ({desconto:0}%) " +
              "ficaria abaixo do custo, por isso o preço foi limitado ao custo unitário."
            : $"Produto parado há {produto.DiasSemVenda} dias: aplicado o desconto padrão da loja " +
              $"para esta faixa ({desconto:0}%).";

        return Montar(produto, "Desconto direto", preco, justificativa,
            MensagemPadrao(produto, preco), geradaPorIA: false, precoAjustado: ajustado);
    }

    /// <summary>
    /// Mensagem padrão de divulgação, com o preço "de/por".
    /// </summary>
    public string MensagemPadrao(Produto produto, decimal precoPromocional)
    {
        var de = produto.PrecoVenda.ToString("C", CulturaBr);
        var por = precoPromocional.ToString("C", CulturaBr);

        return $"🔥 Promoção na nossa loja! {produto.NomeProduto} de {de} por apenas {por}. " +
               $"Estoque limitado: {produto.QuantidadeEstoque} unidades. Venha garantir o seu! 🛒";
    }
}
