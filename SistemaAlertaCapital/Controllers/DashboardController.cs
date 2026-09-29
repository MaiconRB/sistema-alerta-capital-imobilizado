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
    private readonly MensagemPromocionalService _mensagemService;
    private readonly ProdutoRepository _produtoRepository;

    // Os serviços são recebidos por injeção de dependência (registrados no Program.cs)
    public DashboardController(
        CapitalImobilizadoService capitalService,
        MensagemPromocionalService mensagemService,
        ProdutoRepository produtoRepository)
    {
        _capitalService = capitalService;
        _mensagemService = mensagemService;
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
    /// Gera uma mensagem promocional (via IA) para o produto informado.
    /// Chamada pelo JavaScript do painel ao clicar em "Gerar mensagem".
    /// Acesso: POST /Dashboard/GerarMensagem/{id}
    /// [ValidateAntiForgeryToken] exige o token antiforgery enviado pela página,
    /// impedindo que outros sites disparem essa ação em nome do usuário.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarMensagem(int id)
    {
        // Busca o produto no banco; se não existir, responde 404
        var produto = await _produtoRepository.ObterPorIdAsync(id);
        if (produto is null)
        {
            return NotFound();
        }

        // Gera a mensagem (IA ou padrão) e devolve em JSON:
        // { "mensagem": "...", "geradaPorIA": true/false }
        // RequestAborted: interrompe a chamada à IA se o usuário sair da página
        var resultado = await _mensagemService.GerarAsync(produto, HttpContext.RequestAborted);
        return Json(resultado);
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
