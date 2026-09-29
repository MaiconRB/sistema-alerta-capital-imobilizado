using SistemaAlertaCapital.Data;
using SistemaAlertaCapital.Models;

namespace SistemaAlertaCapital.Services;

/// <summary>
/// Concentra as regras de negócio do sistema: classificar os produtos em
/// "parados" ou "ativos" e calcular o capital imobilizado total.
/// </summary>
public class CapitalImobilizadoService
{
    private readonly ProdutoRepository _produtoRepository;

    // Limite de dias sem venda a partir do qual um produto é considerado parado
    private readonly int _diasLimite;

    public CapitalImobilizadoService(ProdutoRepository produtoRepository, IConfiguration configuracao)
    {
        _produtoRepository = produtoRepository;

        // Lê o limite do appsettings.json; se não estiver configurado, usa 180 dias
        _diasLimite = configuracao.GetValue("DiasLimiteParado", 180);
    }

    /// <summary>
    /// Busca todos os produtos e monta os dados do painel.
    /// </summary>
    public async Task<DashboardViewModel> MontarDashboardAsync()
    {
        // 1. Busca todos os produtos (dias sem venda e valor já calculados no SQL)
        var produtos = (await _produtoRepository.ListarTodosAsync()).ToList();

        // 2. Regra de corte: é "parado" o produto sem venda há MAIS dias que o
        //    limite. Ordena pelo maior valor imobilizado (mais crítico primeiro).
        var parados = produtos
            .Where(p => p.DiasSemVenda > _diasLimite)
            .OrderByDescending(p => p.ValorImobilizado)
            .ToList();

        // 3. Monta o resumo: total imobilizado e contagem de parados/ativos
        return new DashboardViewModel
        {
            ProdutosParados = parados,
            TotalImobilizado = parados.Sum(p => p.ValorImobilizado),
            QuantidadeParados = parados.Count,
            QuantidadeAtivos = produtos.Count - parados.Count,
            DiasLimite = _diasLimite
        };
    }
}
