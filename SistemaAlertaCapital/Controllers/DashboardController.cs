using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertaCapital.Data;
using SistemaAlertaCapital.Models;
using SistemaAlertaCapital.Services;

namespace SistemaAlertaCapital.Controllers;

/// <summary>
/// Controller do painel de Capital Imobilizado.
/// Recebe as requisições do navegador, aciona o serviço de regras de negócio
/// e entrega os dados prontos para a View.
/// </summary>
public class DashboardController : Controller
{
    private readonly CapitalImobilizadoService _capitalService;
    private readonly SugestaoPromocaoService _sugestaoService;
    private readonly ProdutoRepository _produtoRepository;

    // Os serviços são recebidos por injeção de dependência (registrados no Program.cs)
    public DashboardController(
        CapitalImobilizadoService capitalService,
        SugestaoPromocaoService sugestaoService,
        ProdutoRepository produtoRepository)
    {
        _capitalService = capitalService;
        _sugestaoService = sugestaoService;
        _produtoRepository = produtoRepository;
    }

    /// <summary>
    /// Página principal: exibe o painel com o resumo e a lista de produtos parados.
    /// Acesso: GET / ou GET /Dashboard
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var dashboard = await _capitalService.MontarDashboardAsync();
        return View(dashboard);
    }

    /// <summary>
    /// Gera uma sugestão de promoção (via IA) para o produto informado:
    /// tipo de ação, preço promocional, indicadores e mensagem de divulgação.
    /// Chamada pelo JavaScript do painel ao clicar em "Sugerir promoção".
    /// Acesso: POST /Dashboard/SugerirPromocao/{id}
    /// [ValidateAntiForgeryToken] exige o token antiforgery enviado pela página,
    /// impedindo que outros sites disparem essa ação em nome do usuário.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SugerirPromocao(int id)
    {
        // Busca o produto no banco; se não existir, responde 404
        var produto = await _produtoRepository.ObterPorIdAsync(id);
        if (produto is null)
        {
            return NotFound();
        }

        // Gera a sugestão (IA ou padrão), já validada pelas regras da loja,
        // e devolve em JSON (campos do record SugestaoPromocao).
        // RequestAborted: interrompe a chamada à IA se o usuário sair da página
        var sugestao = await _sugestaoService.GerarAsync(produto, HttpContext.RequestAborted);
        return Json(sugestao);
    }

    /// <summary>
    /// Página de erro amigável, usada fora do ambiente de desenvolvimento
    /// (configurada em app.UseExceptionHandler no Program.cs).
    /// O cache é desativado para que a página de erro nunca fique armazenada.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
