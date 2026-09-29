# Sistema de Alerta de Capital Imobilizado

Painel web para uma loja de ferragens que identifica **produtos parados há muito tempo**
(sem venda há mais de 180 dias) e calcula o **valor financeiro imobilizado** em estoque
(`quantidade em estoque × custo unitário`). Para cada produto parado, o sistema gera uma
**mensagem promocional com IA** (Google Gemini) para ajudar a girar o estoque.

> Projeto acadêmico de extensão. **Todos os dados são fictícios.**

---

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Aplicação web | ASP.NET Core MVC (.NET 10) com views Razor e Bootstrap |
| Banco de dados | MySQL 8.4 em container Docker |
| Acesso a dados | Dapper (micro-ORM) + MySqlConnector |
| Inteligência artificial | Google Gemini (API REST `generateContent`) via `HttpClient` |

---

## Arquitetura

```
Navegador ──► DashboardController ──► CapitalImobilizadoService ──► ProdutoRepository ──► MySQL
                      │                  (regras de negócio)          (SQL com Dapper)
                      │
                      └──────────────► MensagemPromocionalService ──► API do Gemini
                                         (prompt, nova tentativa, tempo limite, mensagem padrão)
```

| Pasta / arquivo | Responsabilidade |
|---|---|
| `setup_banco.sql` | Cria o banco, a tabela `produtos` e insere 30 produtos fictícios |
| `docker-compose.yml` | Sobe o MySQL e executa o `setup_banco.sql` automaticamente |
| `SistemaAlertaCapital/Models/` | `Produto` (espelha a tabela) e `DashboardViewModel` (dados da tela) |
| `SistemaAlertaCapital/Data/` | `ProdutoRepository`: consultas SQL ao MySQL |
| `SistemaAlertaCapital/Services/` | `CapitalImobilizadoService` (classificação e cálculo) e `MensagemPromocionalService` (IA) |
| `SistemaAlertaCapital/Controllers/` | `DashboardController`: painel, geração de mensagem e página de erro |
| `SistemaAlertaCapital/Views/Dashboard/` | `Index.cshtml`: cartões de resumo, tabela e modal da mensagem |
| `SistemaAlertaCapital/Program.cs` | Configuração: conexão, injeção de dependência, cultura pt-BR e rotas |

### Regras de negócio
- **Dias sem venda:** `DATEDIFF(CURDATE(), data_ultima_venda)` (calculado no SQL).
- **Valor imobilizado:** `quantidade_estoque * custo_unitario` (calculado no SQL).
- **Produto parado:** dias sem venda **maior que** o limite (`DiasLimiteParado`, padrão 180).
- **Destaque na tela:** amarelo acima do limite; vermelho acima de 365 dias.

### Integração com IA
- O botão **Gerar mensagem** envia um `POST` (protegido por token antiforgery) para
  `/Dashboard/GerarMensagem/{id}`.
- O serviço envia ao Gemini o nome do produto, o estoque e os dias sem venda. O custo
  **não** é enviado, e o prompt proíbe inventar preços ou descontos.
- Se o Gemini responder **503** (sobrecarregado), é feita **uma nova tentativa** após 2 s.
- Todo o clique respeita um **tempo limite total** (`TempoLimiteSegundos`, padrão 30 s).
- Se não houver chave, se ocorrer erro ou se o tempo acabar, é exibida uma **mensagem padrão**,
  e o painel continua funcionando.

---

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (aberto e em execução)
- Chave da API do Gemini (opcional, gratuita): <https://aistudio.google.com/apikey>

---

## Como executar

Todos os comandos abaixo devem ser executados na **pasta raiz** do projeto.

### 1. Subir o banco de dados

```powershell
docker compose up -d
```

Na primeira execução, o MySQL cria o banco e insere os 30 produtos automaticamente
(pode levar cerca de 30 segundos).

### 2. Configurar os segredos (apenas na primeira vez)

As senhas e chaves **não** ficam em arquivos do projeto. Elas são guardadas no
*User Secrets* do .NET, no perfil do seu usuário do Windows:

```powershell
# Senha do MySQL (a mesma definida no docker-compose.yml)
dotnet user-secrets set "MySql:Senha" "dev123" --project SistemaAlertaCapital

# Chave do Gemini (opcional; sem ela, o sistema usa a mensagem padrão)
dotnet user-secrets set "IA:ChaveApi" "SUA-CHAVE-AQUI" --project SistemaAlertaCapital
```

### 3. Executar a aplicação

```powershell
dotnet run --project SistemaAlertaCapital
```

Abra no navegador o endereço exibido no terminal (por padrão, <http://localhost:5289>).

### Comandos úteis do banco

| Comando | Efeito |
|---|---|
| `docker compose up -d` | Liga o banco |
| `docker compose down` | Desliga o banco (os dados são mantidos) |
| `docker compose down -v` | Desliga e **apaga** os dados; o `setup_banco.sql` roda de novo na próxima subida |

---

## Configurações (`SistemaAlertaCapital/appsettings.json`)

| Chave | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings:MySql` | `Server=localhost;Port=3306;...` | Conexão com o MySQL (sem a senha) |
| `DiasLimiteParado` | `180` | Dias sem venda para considerar um produto parado |
| `IA:Modelo` | `gemini-3.5-flash-lite` | Modelo do Gemini ([lista de modelos](https://ai.google.dev/gemini-api/docs/models)) |
| `IA:MaxTokens` | `2048` | Tamanho máximo da resposta da IA |
| `IA:TempoLimiteSegundos` | `30` | Tempo máximo total para gerar uma mensagem |

Para testar outro valor sem editar o arquivo, use uma variável de ambiente
(o `__` substitui o `:`), por exemplo:

```powershell
$env:IA__Modelo = "gemini-3.8-flash"; dotnet run --project SistemaAlertaCapital
```

---

## Solução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| Página de erro ao abrir o painel | MySQL desligado | Abra o Docker Desktop e rode `docker compose up -d` |
| `Access denied for user 'root'` | Senha não configurada ou incorreta | Refaça o passo 2 (`MySql:Senha`) |
| Mensagem aparece como "Mensagem padrão (IA indisponível)" | Sem chave, chave inválida, cota excedida ou Gemini sobrecarregado (503) | Confira a chave no passo 2; veja o motivo exato no log do terminal; se persistir, troque `IA:Modelo` |
| Acentos aparecem como `Ã©` | Banco criado com uma versão antiga do `setup_banco.sql` | Rode `docker compose down -v` e depois `docker compose up -d` |
| `docker: failed to connect to the docker API` | Docker Desktop fechado | Abra o Docker Desktop e aguarde ele iniciar |
