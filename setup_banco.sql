-- =============================================================================
-- Sistema de Alerta de Capital Imobilizado
-- Script de configuração do banco de dados (MySQL)
--
-- Objetivo: criar a estrutura da tabela de produtos e popular com 30 itens
-- fictícios de uma loja de ferragens, simulando produtos com vendas recentes
-- e produtos parados há mais de 180 dias.
--
-- Observação: todos os dados são FICTÍCIOS e têm finalidade acadêmica.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 0. Codificação da conexão
--    Informa ao MySQL que este arquivo está em UTF-8. Sem isso, alguns
--    clientes (como o do container Docker) leem o texto como latin1 e os
--    acentos são gravados corrompidos (ex.: "é" vira "Ã©").
-- -----------------------------------------------------------------------------
SET NAMES utf8mb4;


-- -----------------------------------------------------------------------------
-- 1. Criação e seleção do banco de dados
--    O "IF NOT EXISTS" evita erro caso o banco já exista.
--    utf8mb4 garante suporte completo a acentuação em português.
-- -----------------------------------------------------------------------------
CREATE DATABASE IF NOT EXISTS alerta_capital_imobilizado
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE alerta_capital_imobilizado;


-- -----------------------------------------------------------------------------
-- 2. Remoção da tabela (caso exista)
--    Permite executar o script várias vezes, sempre partindo de um estado limpo.
-- -----------------------------------------------------------------------------
DROP TABLE IF EXISTS produtos;


-- -----------------------------------------------------------------------------
-- 3. Criação da tabela "produtos"
--    - id_produto:         identificador único, gerado automaticamente
--    - nome_produto:       descrição do item de ferragem
--    - quantidade_estoque: unidades disponíveis em estoque (não negativo)
--    - custo_unitario:     custo de aquisição por unidade (DECIMAL evita
--                          erros de arredondamento em valores monetários)
--    - preco_venda:        preço de venda atual ao cliente (sem promoção)
--    - data_ultima_venda:  data em que o produto foi vendido pela última vez
--    A restrição CHECK impede cadastrar um preço de venda menor que o custo.
-- -----------------------------------------------------------------------------
CREATE TABLE produtos (
    id_produto          INT            NOT NULL AUTO_INCREMENT,
    nome_produto        VARCHAR(100)   NOT NULL,
    quantidade_estoque  INT UNSIGNED   NOT NULL DEFAULT 0,
    custo_unitario      DECIMAL(10, 2) NOT NULL,
    preco_venda         DECIMAL(10, 2) NOT NULL,
    data_ultima_venda   DATE           NOT NULL,
    PRIMARY KEY (id_produto),
    CONSTRAINT chk_preco_maior_que_custo CHECK (preco_venda >= custo_unitario)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;


-- -----------------------------------------------------------------------------
-- 4. Inserção dos 30 produtos fictícios
--    As datas são calculadas a partir da data atual (CURDATE()) usando
--    DATE_SUB. Assim, a simulação continua válida independentemente do dia
--    em que o script for executado.
--    Os preços de venda usam margens fictícias entre 30% e 90% sobre o custo,
--    arredondados para finais ,90 (prática comum no varejo).
-- -----------------------------------------------------------------------------

-- 4.1 Produtos com VENDA RECENTE (menos de 180 dias sem venda) — 15 itens
--     Inclui alguns casos próximos do limite (ex.: 150 e 175 dias) para testar
--     a regra de corte.
INSERT INTO produtos (nome_produto, quantidade_estoque, custo_unitario, preco_venda, data_ultima_venda) VALUES
    ('Parafuso Sextavado 1/4" x 2" (cento)',     120,   18.50,    29.90, DATE_SUB(CURDATE(), INTERVAL 2   DAY)),
    ('Fita Veda Rosca 18mm x 50m',                200,    4.20,     7.90, DATE_SUB(CURDATE(), INTERVAL 1   DAY)),
    ('Martelo Unha 27mm Cabo de Madeira',          35,   32.90,    49.90, DATE_SUB(CURDATE(), INTERVAL 7   DAY)),
    ('Chave de Fenda 1/4" x 6"',                   60,    9.80,    16.90, DATE_SUB(CURDATE(), INTERVAL 12  DAY)),
    ('Prego 17x27 com Cabeça (kg)',                80,   14.30,    20.90, DATE_SUB(CURDATE(), INTERVAL 3   DAY)),
    ('Bucha Nylon 8mm (cento)',                   150,    7.60,    13.90, DATE_SUB(CURDATE(), INTERVAL 5   DAY)),
    ('Trena de Aço 5m',                            40,   21.40,    33.90, DATE_SUB(CURDATE(), INTERVAL 20  DAY)),
    ('Lixa d''Água Grão 220',                     300,    1.35,     2.50, DATE_SUB(CURDATE(), INTERVAL 9   DAY)),
    ('Disco de Corte Inox 4.1/2"',                 90,    5.75,     9.90, DATE_SUB(CURDATE(), INTERVAL 15  DAY)),
    ('Cadeado Latão 30mm',                         45,   19.90,    29.90, DATE_SUB(CURDATE(), INTERVAL 33  DAY)),
    ('Broca Aço Rápido 6mm',                       70,    6.40,    10.90, DATE_SUB(CURDATE(), INTERVAL 48  DAY)),
    ('Silicone Acético Transparente 280g',         55,   16.80,    24.90, DATE_SUB(CURDATE(), INTERVAL 62  DAY)),
    ('Alicate Universal 8"',                       25,   38.50,    53.90, DATE_SUB(CURDATE(), INTERVAL 95  DAY)),
    ('Dobradiça de Ferro 3" (par)',                65,   11.20,    17.90, DATE_SUB(CURDATE(), INTERVAL 150 DAY)),
    ('Arame Recozido nº 18 (kg)',                  50,   17.60,    23.90, DATE_SUB(CURDATE(), INTERVAL 175 DAY));

-- 4.2 Produtos PARADOS (mais de 180 dias sem venda) — 15 itens
--     Variação de 185 dias até mais de 2 anos, com custos altos e baixos,
--     para gerar diferentes níveis de capital imobilizado.
INSERT INTO produtos (nome_produto, quantidade_estoque, custo_unitario, preco_venda, data_ultima_venda) VALUES
    ('Fechadura Digital Biométrica',                8,  489.00,   689.90, DATE_SUB(CURDATE(), INTERVAL 185 DAY)),
    ('Serra Circular de Bancada 10"',               3, 1250.00,  1629.90, DATE_SUB(CURDATE(), INTERVAL 240 DAY)),
    ('Torno de Bancada nº 6',                       5,  315.00,   459.90, DATE_SUB(CURDATE(), INTERVAL 310 DAY)),
    ('Jogo de Chaves Torx 9 Peças',                22,   42.70,    68.90, DATE_SUB(CURDATE(), INTERVAL 200 DAY)),
    ('Rebitadeira Pneumática',                      4,  389.90,   529.90, DATE_SUB(CURDATE(), INTERVAL 420 DAY)),
    ('Dobradiça Invisível para Porta Pivotante',   30,   67.50,   104.90, DATE_SUB(CURDATE(), INTERVAL 365 DAY)),
    ('Parafuso Francês 5/8" x 10" (cento)',        12,   96.00,   144.90, DATE_SUB(CURDATE(), INTERVAL 530 DAY)),
    ('Talha Manual de Corrente 1 Tonelada',         2,  720.00,   939.90, DATE_SUB(CURDATE(), INTERVAL 610 DAY)),
    ('Puxador de Latão Envelhecido 30cm',          40,   28.90,    49.90, DATE_SUB(CURDATE(), INTERVAL 275 DAY)),
    ('Macaco Hidráulico Jacaré 3 Toneladas',        6,  298.00,   419.90, DATE_SUB(CURDATE(), INTERVAL 190 DAY)),
    ('Kit Brocas Diamantadas para Vidro',          15,   54.30,    89.90, DATE_SUB(CURDATE(), INTERVAL 455 DAY)),
    ('Soprador Térmico 2000W',                      7,  176.40,   259.90, DATE_SUB(CURDATE(), INTERVAL 330 DAY)),
    ('Corrente Galvanizada 8mm (metro)',          150,   12.80,    22.90, DATE_SUB(CURDATE(), INTERVAL 220 DAY)),
    ('Nível a Laser Autonivelante',                 4,  540.00,   819.90, DATE_SUB(CURDATE(), INTERVAL 700 DAY)),
    ('Grampo Sargento 24"',                        18,   49.90,    79.90, DATE_SUB(CURDATE(), INTERVAL 260 DAY));


-- -----------------------------------------------------------------------------
-- 5. Conferência rápida (opcional)
--    Lista os produtos com os dias sem venda, apenas para validar a carga.
-- -----------------------------------------------------------------------------
SELECT
    id_produto,
    nome_produto,
    quantidade_estoque,
    custo_unitario,
    data_ultima_venda,
    DATEDIFF(CURDATE(), data_ultima_venda) AS dias_sem_venda
FROM produtos
ORDER BY dias_sem_venda DESC;
