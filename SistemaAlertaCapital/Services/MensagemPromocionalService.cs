using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SistemaAlertaCapital.Models;

namespace SistemaAlertaCapital.Services;

/// <summary>
/// Resultado da geração de uma mensagem promocional.
/// </summary>
/// <param name="Mensagem">Texto promocional pronto para uso.</param>
/// <param name="GeradaPorIA">
/// true se o texto veio da IA; false se foi usada a mensagem padrão (fallback).
/// </param>
public record ResultadoMensagem(string Mensagem, bool GeradaPorIA);

/// <summary>
/// Gera mensagens promocionais para produtos parados usando a API do Google Gemini.
/// É um "typed HttpClient": o HttpClient é criado e gerenciado pelo .NET
/// (registrado com AddHttpClient no Program.cs), evitando esgotar conexões.
/// Se a IA não estiver disponível, devolve uma mensagem padrão.
/// </summary>
public class MensagemPromocionalService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MensagemPromocionalService> _logger;

    // Configurações lidas do appsettings.json (seção "IA") e do User Secrets
    private readonly string _endpoint;
    private readonly string _modelo;
    private readonly int _maxTokens;
    private readonly TimeSpan _tempoLimite;
    private readonly string? _chaveApi;

    // Cultura brasileira para formatar os números inseridos no prompt
    private static readonly CultureInfo CulturaBr = new("pt-BR");

    // Nova tentativa quando o Gemini responde 503 (modelo sobrecarregado):
    // no máximo 2 tentativas no total, com 2 segundos de espera entre elas
    private const int MaxTentativas = 2;
    private static readonly TimeSpan EsperaEntreTentativas = TimeSpan.FromSeconds(2);

    public MensagemPromocionalService(
        HttpClient httpClient,
        IConfiguration configuracao,
        ILogger<MensagemPromocionalService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _endpoint = configuracao["IA:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/models";
        _modelo = configuracao["IA:Modelo"] ?? "gemini-3.5-flash-lite";
        _maxTokens = configuracao.GetValue("IA:MaxTokens", 2048);

        // Tempo máximo TOTAL de um clique (todas as tentativas somadas)
        _tempoLimite = TimeSpan.FromSeconds(configuracao.GetValue("IA:TempoLimiteSegundos", 30));

        // A chave NUNCA fica no appsettings.json; vem do User Secrets
        _chaveApi = configuracao["IA:ChaveApi"];
    }

    /// <summary>
    /// Gera uma mensagem promocional para o produto informado.
    /// Tenta usar a IA; em caso de falha, retorna a mensagem padrão.
    /// </summary>
    /// <param name="produto">Produto para o qual a mensagem será gerada.</param>
    /// <param name="cancelamentoExterno">
    /// Sinal de cancelamento da requisição do navegador (HttpContext.RequestAborted):
    /// se o usuário fechar ou recarregar a página, a chamada à IA é interrompida.
    /// </param>
    public async Task<ResultadoMensagem> GerarAsync(Produto produto, CancellationToken cancelamentoExterno = default)
    {
        // 1. Sem chave configurada: nem tenta chamar a IA
        if (string.IsNullOrWhiteSpace(_chaveApi))
        {
            _logger.LogWarning("Chave da IA (IA:ChaveApi) não configurada. Usando mensagem padrão.");
            return new ResultadoMensagem(MontarMensagemPadrao(produto), false);
        }

        // Prazo único para todo o clique: combina o tempo limite configurado
        // com o cancelamento vindo do navegador. O que ocorrer primeiro cancela.
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelamentoExterno);
        prazo.CancelAfter(_tempoLimite);

        try
        {
            // 2. Chama a API do Gemini (todas as tentativas respeitam o mesmo prazo)
            var texto = await ChamarGeminiAsync(MontarPrompt(produto), prazo.Token);

            // 3. Resposta vazia também é tratada como falha
            if (string.IsNullOrWhiteSpace(texto))
            {
                _logger.LogWarning("A IA retornou uma resposta vazia. Usando mensagem padrão.");
                return new ResultadoMensagem(MontarMensagemPadrao(produto), false);
            }

            return new ResultadoMensagem(texto.Trim(), true);
        }
        catch (OperationCanceledException) when (cancelamentoExterno.IsCancellationRequested)
        {
            // 4a. O usuário saiu da página: ninguém vai receber a resposta
            _logger.LogInformation("Requisição cancelada pelo navegador. Chamada à IA interrompida.");
            return new ResultadoMensagem(MontarMensagemPadrao(produto), false);
        }
        catch (OperationCanceledException) when (prazo.IsCancellationRequested)
        {
            // 4b. O tempo limite total foi atingido
            _logger.LogWarning("Tempo limite da IA atingido ({Segundos} s). Usando mensagem padrão.",
                _tempoLimite.TotalSeconds);
            return new ResultadoMensagem(MontarMensagemPadrao(produto), false);
        }
        catch (Exception ex)
        {
            // 4c. Qualquer outro erro (rede, chave inválida, cota
            //    excedida...) não derruba o painel: registra no log e usa o padrão
            _logger.LogError(ex, "Falha ao gerar mensagem com a IA. Usando mensagem padrão.");
            return new ResultadoMensagem(MontarMensagemPadrao(produto), false);
        }
    }

    /// <summary>
    /// Monta o texto de instrução (prompt) enviado à IA, com os dados do produto.
    /// O custo unitário NÃO é enviado de propósito: é um dado interno da loja e
    /// poderia levar a IA a sugerir preços, o que as regras do prompt proíbem.
    /// </summary>
    private static string MontarPrompt(Produto produto)
    {
        return string.Format(CulturaBr,
            """
            Você é o redator de marketing de uma loja de ferragens de bairro.
            Escreva UMA mensagem promocional curta (no máximo 4 linhas) para divulgar
            no WhatsApp e nas redes sociais o produto abaixo, que está parado no estoque.

            Produto: {0}
            Unidades em estoque: {1}
            Dias sem venda: {2}

            Regras:
            - Escreva em português do Brasil, com tom amigável e chamativo.
            - Pode usar até 2 emojis.
            - NÃO invente preço, percentual de desconto ou prazo da promoção.
            - Destaque a utilidade do produto e convide o cliente a visitar a loja.
            - Responda apenas com o texto da mensagem, sem comentários adicionais.
            """,
            produto.NomeProduto, produto.QuantidadeEstoque, produto.DiasSemVenda);
    }

    /// <summary>
    /// Envia o prompt ao endpoint generateContent do Gemini e extrai o texto da resposta.
    /// Documentação: https://ai.google.dev/api/generate-content
    /// </summary>
    private async Task<string> ChamarGeminiAsync(string prompt, CancellationToken cancelamento)
    {
        // Corpo da requisição no formato esperado pela API do Gemini
        var corpo = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { maxOutputTokens = _maxTokens }
        };

        var corpoJson = JsonSerializer.Serialize(corpo);
        string json;

        for (var tentativa = 1; ; tentativa++)
        {
            // Uma requisição HTTP não pode ser reenviada; por isso é recriada
            // a cada tentativa. URL: {endpoint}/{modelo}:generateContent
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/{_modelo}:generateContent")
            {
                Content = new StringContent(corpoJson, Encoding.UTF8, "application/json")
            };

            // A chave vai no cabeçalho (e não na URL) para não aparecer em logs
            requisicao.Headers.Add("x-goog-api-key", _chaveApi);

            using var resposta = await _httpClient.SendAsync(requisicao, cancelamento);
            json = await resposta.Content.ReadAsStringAsync(cancelamento);

            // 503 = modelo sobrecarregado (falha temporária): espera e tenta de novo
            if (resposta.StatusCode == HttpStatusCode.ServiceUnavailable && tentativa < MaxTentativas)
            {
                _logger.LogWarning("Gemini sobrecarregado (503). Nova tentativa em {Segundos} s.",
                    EsperaEntreTentativas.TotalSeconds);
                await Task.Delay(EsperaEntreTentativas, cancelamento);
                continue;
            }

            // Qualquer outro erro (ex.: 400, 403, 429) ou 503 na última
            // tentativa: gera exceção com o detalhe retornado pela API
            if (!resposta.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Gemini respondeu {(int)resposta.StatusCode}: {json}");
            }

            break; // Sucesso: sai do laço e segue para ler a resposta
        }

        // Estrutura da resposta: candidates[0].content.parts[].text
        using var documento = JsonDocument.Parse(json);
        var texto = new StringBuilder();

        if (documento.RootElement.TryGetProperty("candidates", out var candidatos)
            && candidatos.GetArrayLength() > 0
            && candidatos[0].TryGetProperty("content", out var conteudo)
            && conteudo.TryGetProperty("parts", out var partes))
        {
            foreach (var parte in partes.EnumerateArray())
            {
                // Ignora partes de "raciocínio" do modelo, se houver; usa só o texto final
                var ehRaciocinio = parte.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.True;
                if (!ehRaciocinio && parte.TryGetProperty("text", out var trecho))
                {
                    texto.Append(trecho.GetString());
                }
            }
        }

        return texto.ToString();
    }

    /// <summary>
    /// Mensagem padrão (fallback), usada quando a IA não está disponível.
    /// </summary>
    private static string MontarMensagemPadrao(Produto produto)
    {
        return $"🔧 Oportunidade na nossa loja! Temos {produto.NomeProduto} disponível em estoque " +
               $"({produto.QuantidadeEstoque} unidades). Venha conferir condições especiais " +
               "e garanta o seu! 🛒";
    }
}
