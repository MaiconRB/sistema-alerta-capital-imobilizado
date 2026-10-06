using Dapper;
using MySqlConnector;
using SistemaAlertaCapital.Models;

namespace SistemaAlertaCapital.Data;

/// <summary>
/// Responsável por todo o acesso à tabela "produtos" no MySQL.
/// Utiliza o micro-ORM Dapper, que executa o SQL escrito aqui e converte
/// automaticamente cada linha do resultado em um objeto <see cref="Produto"/>.
/// </summary>
public class ProdutoRepository
{
    // String de conexão com o MySQL, recebida via injeção de dependência
    private readonly string _connectionString;

    // -------------------------------------------------------------------------
    // Trecho SELECT comum às consultas.
    // - Os apelidos (AS) fazem cada coluna do banco corresponder ao nome de uma
    //   propriedade da classe Produto, permitindo o mapeamento automático.
    // - DATEDIFF(CURDATE(), data_ultima_venda) calcula os dias sem venda.
    // - quantidade_estoque * custo_unitario calcula o capital imobilizado.
    // -------------------------------------------------------------------------
    private const string SelectBase = @"
        SELECT
            id_produto                               AS IdProduto,
            nome_produto                             AS NomeProduto,
            quantidade_estoque                       AS QuantidadeEstoque,
            custo_unitario                           AS CustoUnitario,
            preco_venda                              AS PrecoVenda,
            data_ultima_venda                        AS DataUltimaVenda,
            DATEDIFF(CURDATE(), data_ultima_venda)   AS DiasSemVenda,
            quantidade_estoque * custo_unitario      AS ValorImobilizado
        FROM produtos";

    public ProdutoRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Retorna todos os produtos cadastrados, com os campos calculados
    /// (dias sem venda e valor imobilizado) já preenchidos.
    /// </summary>
    public async Task<IEnumerable<Produto>> ListarTodosAsync()
    {
        // O "using" garante que a conexão seja fechada ao final do método,
        // mesmo que ocorra alguma exceção
        await using var conexao = new MySqlConnection(_connectionString);

        // QueryAsync executa o SELECT e converte cada linha em um Produto
        return await conexao.QueryAsync<Produto>(SelectBase + " ORDER BY id_produto;");
    }

    /// <summary>
    /// Busca um único produto pelo seu identificador.
    /// Retorna null caso o produto não exista.
    /// </summary>
    public async Task<Produto?> ObterPorIdAsync(int idProduto)
    {
        await using var conexao = new MySqlConnection(_connectionString);

        // O valor é enviado como parâmetro (@id), nunca concatenado ao texto
        // do SQL, o que protege a aplicação contra SQL Injection
        return await conexao.QueryFirstOrDefaultAsync<Produto>(
            SelectBase + " WHERE id_produto = @id;",
            new { id = idProduto });
    }
}
