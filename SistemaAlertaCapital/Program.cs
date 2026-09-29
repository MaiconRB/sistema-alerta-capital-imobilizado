using MySqlConnector;
using SistemaAlertaCapital.Data;
using SistemaAlertaCapital.Services;

var builder = WebApplication.CreateBuilder(args);

// Habilita o padrão MVC (Controllers + Views Razor)
builder.Services.AddControllersWithViews();

// -----------------------------------------------------------------------------
// Conexão com o MySQL
// A string base (servidor, porta, banco e usuário) vem do appsettings.json.
// A senha vem do User Secrets (chave "MySql:Senha"), para não ficar gravada
// em arquivos do projeto. As duas partes são unidas pelo builder abaixo.
// -----------------------------------------------------------------------------
var construtorConexao = new MySqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("ConnectionStrings:MySql não configurada no appsettings.json."))
{
    Password = builder.Configuration["MySql:Senha"]
};
var connectionString = construtorConexao.ConnectionString;

// Registra o repository na injeção de dependência (uma instância por requisição)
builder.Services.AddScoped(_ => new ProdutoRepository(connectionString));

// Registra o serviço de regras de negócio (classificação e cálculo)
builder.Services.AddScoped<CapitalImobilizadoService>();

// Registra o serviço de IA como "typed HttpClient": o .NET gerencia o ciclo de
// vida das conexões HTTP. O limite de tempo principal (IA:TempoLimiteSegundos)
// é aplicado no próprio serviço; este timeout de 60 s é apenas uma rede de
// segurança e deve ser sempre MAIOR que IA:TempoLimiteSegundos.
builder.Services.AddHttpClient<MensagemPromocionalService>(cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// Pipeline de requisições HTTP
// Fora do ambiente de desenvolvimento, erros não tratados exibem a página
// amigável /Dashboard/Error, e o HSTS obriga o navegador a usar HTTPS.
// -----------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

// -----------------------------------------------------------------------------
// Cultura pt-BR fixa para toda a aplicação: valores monetários no formato
// "R$ 1.234,56" e datas no formato "dd/MM/aaaa", independentemente do idioma
// configurado no navegador do usuário.
// -----------------------------------------------------------------------------
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures("pt-BR")
    .AddSupportedUICultures("pt-BR"));

app.UseHttpsRedirection();   // Redireciona HTTP -> HTTPS quando disponível
app.UseRouting();            // Ativa o roteamento para as Controllers
app.UseAuthorization();      // Mantido do template (o sistema não possui login)
app.MapStaticAssets();       // Serve os arquivos de wwwroot (CSS, JS, Bootstrap)

// Rota padrão: o endereço raiz "/" abre o painel (DashboardController.Index)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// Inicia a aplicação web
app.Run();
