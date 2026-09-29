namespace SistemaAlertaCapital.Models;

/// <summary>
/// Representa um produto da loja de ferragens, espelhando a tabela "produtos"
/// do MySQL, acrescido de dois campos calculados pela consulta SQL.
/// </summary>
public class Produto
{
    // ---------------------------------------------------------------------
    // Colunas da tabela "produtos"
    // ---------------------------------------------------------------------

    /// <summary>Identificador único do produto (coluna id_produto).</summary>
    public int IdProduto { get; set; }

    /// <summary>Descrição do item de ferragem (coluna nome_produto).</summary>
    public string NomeProduto { get; set; } = string.Empty;

    /// <summary>Unidades disponíveis em estoque (coluna quantidade_estoque).</summary>
    public int QuantidadeEstoque { get; set; }

    /// <summary>Custo de aquisição por unidade, em R$ (coluna custo_unitario).</summary>
    public decimal CustoUnitario { get; set; }

    /// <summary>Data da última venda registrada (coluna data_ultima_venda).</summary>
    public DateTime DataUltimaVenda { get; set; }

    // ---------------------------------------------------------------------
    // Campos calculados (não existem na tabela; são gerados pelo SELECT)
    // ---------------------------------------------------------------------

    /// <summary>Quantidade de dias desde a última venda até hoje.</summary>
    public int DiasSemVenda { get; set; }

    /// <summary>
    /// Valor financeiro parado no estoque: quantidade em estoque × custo unitário.
    /// </summary>
    public decimal ValorImobilizado { get; set; }
}
