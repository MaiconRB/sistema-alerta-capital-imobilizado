using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using SistemaAlertaCapital.Models;

namespace SistemaAlertaCapital.Services;

/// <summary>
/// Gera, com a API do Google Gemini, uma sugestão de promoção para um produto
/// parado: tipo de ação, preço promocional, justificativa e mensagem de divulgação.
///
/// - É um "typed HttpClient": o HttpClient é criado e gerenciado pelo .NET
///   (registrado com AddHttpClient no Program.cs), evitando esgotar conexões.
/// - A resposta da IA vem em JSON estruturado (responseSchema) e SEMPRE passa
///   pelas regras de negócio (RegrasPromocao) antes de chegar à tela.
/// - Se a IA não estiver disponível, devolve a sugestão padrão da loja.
/// </summary>
public class SugestaoPromocaoService
{
    private readonly HttpClient _httpClient;
    private readonly RegrasPromocao _regras;
    private readonly ILogger<SugestaoPromocaoService> _logger;

    // Configurações lidas do appsettings.json (seção "IA") e do User Secrets
    private readonly string _endpoint;
    private readonly string _modelo;
    private readonly int _maxTokens;
    private readonly TimeSpan _tempoLimite;
    private readonly string? _chaveApi;

    // Cultura brasileira para formatar os valores em R$ inseridos no prompt
    private static readonly CultureInfo CulturaBr = new("pt-BR");

    // Nova tentativa quando o Gemini responde 503 (modelo sobrecarregado):
    // no máximo 2 tentativas no total, com 2 segundos de espera entre elas
    private const int MaxTentativas = 2;
    private static readonly TimeSpan EsperaEntreTentativas = TimeSpan.FromSeconds(2);

    // Leitura do JSON da IA sem diferenciar maiúsculas/minúsculas nos nomes dos campos
    private static readonly JsonSerializerOptions OpcoesJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Formato da resposta que a IA deve devolver (definido no responseSchema).</summary>
    private sealed record RespostaIA(string? TipoPromocao, decimal? PrecoPromocional, string? Justificativa, string? Mensagem);

    public SugestaoPromocaoService(
        HttpClient httpClient,
        RegrasPromocao regras,
        IConfiguration configuracao,
        ILogger<SugestaoPromocaoService> logger)
    {
        _httpClient = httpClient;
        _regras = regras;
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
    /// Gera a sugestão de promoção para o produto informado.
    /// Tenta usar a IA; em caso de falha ou resposta inválida, retorna a sugestão padrão.
    /// </summary>
    /// <param name="produto">Produto parado para o qual a promoção será sugerida.</param>
    /// <param name="cancelamentoExterno">
    /// Sinal de cancelamento da requisição do navegador (HttpContext.RequestAborted):
    /// se o usuário fechar ou recarregar a página, a chamada à IA é interrompida.
    /// </param>
    public async Task<SugestaoPromocao> GerarAsync(Produto produto, CancellationToken cancelamentoExterno = default)
    {
        // 1. Sem chave configurada: nem tenta chamar a IA
        if (string.IsNullOrWhiteSpace(_chaveApi))
        {
            _logger.LogWarning("Chave da IA (IA:ChaveApi) não configurada. Usando sugestão padrão.");
            return _regras.SugestaoPadrao(produto);
        }

        // Prazo único para todo o clique: combina o tempo limite configurado
        // com o cancelamento vindo do navegador. O que ocorrer primeiro cancela.
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelamentoExterno);
        prazo.CancelAfter(_tempoLimite);

        try
        {
            // 2. Chama a API do Gemini (todas as tentativas respeitam o mesmo prazo)
            var json = await ChamarGeminiAsync(MontarPrompt(produto), prazo.Token);

            // 3. Converte o JSON da IA e aplica as regras de negócio
            return ValidarResposta(produto, json) ?? _regras.SugestaoPadrao(produto);
        }
        catch (OperationCanceledException) when (cancelamentoExterno.IsCancellationRequested)
        {
            // 4a. O usuário saiu da página: ninguém vai receber a resposta
            _logger.LogInformation("Requisição cancelada pelo navegador. Chamada à IA interrompida.");
            return _regras.SugestaoPadrao(produto);
        }
        catch (OperationCanceledException) when (prazo.IsCancellationRequested)
        {
            // 4b. O tempo limite total foi atingido
            _logger.LogWarning("Tempo limite da IA atingido ({Segundos} s). Usando sugestão padrão.",
                _tempoLimite.TotalSeconds);
            return _regras.SugestaoPadrao(produto);
        }
        catch (Exception ex)
        {
            // 4c. Qualquer outro erro (rede, chave inválida, cota excedida, JSON
            //     malformado...) não derruba o painel: registra no log e usa o padrão
            _logger.LogError(ex, "Falha ao gerar sugestão com a IA. Usando sugestão padrão.");
            return _regras.SugestaoPadrao(produto);
        }
    }

    /// <summary>
    /// Converte a resposta da IA e aplica as regras da loja.
    /// Retorna null se a resposta for inutilizável (o chamador usa então a sugestão padrão).
    /// </summary>
    private SugestaoPromocao? ValidarResposta(Produto produto, string json)
    {
        var resposta = JsonSerializer.Deserialize<RespostaIA>(json, OpcoesJson);

        // Campos obrigatórios ausentes ou vazios: resposta descartada
        if (resposta?.PrecoPromocional is not decimal precoIA
            || string.IsNullOrWhiteSpace(resposta.TipoPromocao)
            || string.IsNullOrWhiteSpace(resposta.Mensagem))
        {
            _logger.LogWarning("Resposta da IA incompleta. Usando sugestão padrão. JSON: {Json}", json);
            return null;
        }

        // Preço que não é promoção (zero, negativo ou maior/igual ao preço atual): descartado
        if (precoIA <= 0 || precoIA >= produto.PrecoVenda)
        {
            _logger.LogWarning("IA sugeriu preço inválido ({Preco}) para preço atual {Atual}. Usando sugestão padrão.",
                precoIA, produto.PrecoVenda);
            return null;
        }

        // Regra do piso: nunca abaixo do custo
        var (preco, ajustado) = _regras.AplicarPiso(produto, precoIA);
        var mensagem = resposta.Mensagem.Trim();
        var justificativa = resposta.Justificativa?.Trim() ?? string.Empty;

        if (ajustado)
        {
            // A mensagem da IA citava um preço abaixo do custo: é substituída pela
            // mensagem padrão com o preço corrigido, para não divulgar valor errado
            _logger.LogWarning("IA sugeriu {PrecoIA} abaixo do custo {Custo}. Preço elevado ao piso.",
                precoIA, produto.CustoUnitario);
            mensagem = _regras.MensagemPadrao(produto, preco);
            justificativa += " (Preço ajustado pelo sistema para não ficar abaixo do custo.)";
        }

        return _regras.Montar(produto, resposta.TipoPromocao.Trim(), preco, justificativa,
            mensagem, geradaPorIA: true, precoAjustado: ajustado);
    }

    /// <summary>
    /// Monta o texto de instrução (prompt) enviado à IA, com os dados do produto.
    /// O custo e o preço mínimo são enviados para que a IA respeite a margem da loja,
    /// mas o prompt proíbe que eles apareçam na mensagem ao cliente.
    /// </summary>
    private string MontarPrompt(Produto produto)
    {
        return string.Format(CulturaBr,
            """
            Você é um consultor comercial de uma loja de ferragens de bairro.
            O produto abaixo está parado no estoque e precisa ser vendido para liberar capital.

            Produto: {0}
            Unidades em estoque: {1}
            Dias sem venda: {2}
            Preço de venda atual: {3:C}
            Custo unitário (dado interno): {4:C}
            Preço mínimo permitido: {5:C}
            Capital imobilizado: {6:C}
            Desconto de referência da loja para esta faixa de dias: {7:0}%

            Sugira UMA promoção para girar esse estoque. Tipos possíveis:
            "Desconto direto", "Leve X pague Y", "Combo com produto complementar" ou "Queima de estoque".

            Regras:
            - "precoPromocional" é o preço UNITÁRIO EFETIVO pago pelo cliente (em "Leve 3 pague 2",
              por exemplo, é 2/3 do preço atual). Use número com ponto decimal, sem "R$".
            - "precoPromocional" deve ser MAIOR OU IGUAL ao preço mínimo permitido e MENOR que o preço atual.
            - Quanto mais tempo parado e maior o capital imobilizado, mais agressiva pode ser a promoção.
            - "justificativa": 1 a 2 frases explicando a escolha para o gerente da loja.
            - "mensagem": texto curto (até 4 linhas) para WhatsApp e redes sociais, em português do Brasil,
              tom amigável, até 2 emojis, citando o preço "de/por" com os valores exatos em reais.
            - NUNCA mencione o custo, a margem ou o preço mínimo na mensagem ao cliente.
            """,
            produto.NomeProduto, produto.QuantidadeEstoque, produto.DiasSemVenda,
            produto.PrecoVenda, produto.CustoUnitario, _regras.PrecoMinimo(produto),
            produto.ValorImobilizado, _regras.DescontoPadraoPercentual(produto.DiasSemVenda));
    }

    /// <summary>
    /// Envia o prompt ao endpoint generateContent do Gemini e devolve o JSON gerado.
    /// O responseSchema obriga o modelo a responder exatamente nos campos esperados.
    /// Documentação: https://ai.google.dev/gemini-api/docs/structured-output
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
            generationConfig = new
            {
                maxOutputTokens = _maxTokens,
                // Saída estruturada: a resposta vem em JSON com os campos abaixo
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new Dictionary<string, object>
                    {
                        ["tipoPromocao"] = new { type = "STRING" },
                        ["precoPromocional"] = new { type = "NUMBER" },
                        ["justificativa"] = new { type = "STRING" },
                        ["mensagem"] = new { type = "STRING" }
                    },
                    required = new[] { "tipoPromocao", "precoPromocional", "justificativa", "mensagem" }
                }
            }
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
}
