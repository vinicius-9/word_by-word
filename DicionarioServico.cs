using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace WordByWord
{
    /// <summary>
    /// Guarda as palavras cadastradas em um banco SQLite local (dicionario.db), em vez do
    /// antigo arquivo dicionario.json. A interface pública (Carregar/Salvar/Adicionar/...)
    /// continua igual de propósito — o resto do app (MainWindow etc.) não precisa mudar nada.
    ///
    /// Padrão usado: "Palavras" é a lista viva em memória. Carregar() lê tudo do banco pra
    /// essa lista; Salvar() regrava a tabela inteira a partir da lista atual. É o mesmo
    /// comportamento "carrega tudo / salva tudo" que já existia com o JSON, só que agora
    /// gravado em um banco de verdade (mais robusto contra arquivo corrompido, permite
    /// consultas melhores no futuro, etc.).
    /// </summary>
    public class DicionarioServico
    {
        private const string ArquivoBanco = "dicionario.db";
        private const string ArquivoJsonAntigo = "dicionario.json";
        private static readonly string ConnectionString = $"Data Source={ArquivoBanco}";

        public List<Palavra> Palavras { get; set; } = new();

        // ── Infraestrutura do banco ──

        private static SqliteConnection AbrirConexao()
        {
            var conexao = new SqliteConnection(ConnectionString);
            conexao.Open();
            return conexao;
        }

        private static void GarantirBanco()
        {
            using var conexao = AbrirConexao();
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Palavras (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    PalavraTexto    TEXT NOT NULL,
                    Significado     TEXT,
                    Exemplo         TEXT,
                    Traducao        TEXT,
                    IdeiaPrincipal  TEXT,
                    SignificadosJson TEXT,
                    ExemplosJson     TEXT,
                    ExpressoesJson   TEXT,
                    SentidosJson     TEXT,
                    Dica             TEXT
                );";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Migração automática: se o banco ainda não tem nenhuma palavra e existe um
        /// dicionario.json de uma versão antiga do app, importa tudo dele uma única vez.
        /// Depois disso o .json deixa de ser usado (o banco passa a ser a fonte de verdade).
        /// </summary>
        private static void MigrarDoJsonSeNecessario()
        {
            if (!File.Exists(ArquivoJsonAntigo))
                return;

            using (var conexao = AbrirConexao())
            using (var cmdConta = conexao.CreateCommand())
            {
                cmdConta.CommandText = "SELECT COUNT(*) FROM Palavras";
                var total = (long)(cmdConta.ExecuteScalar() ?? 0L);
                if (total > 0)
                    return; // banco já tem dados — não migra de novo
            }

            List<Palavra>? antigas;
            try
            {
                var json = File.ReadAllText(ArquivoJsonAntigo);
                antigas = JsonSerializer.Deserialize<List<Palavra>>(json);
            }
            catch
            {
                return; // json antigo corrompido/ilegível — segue só com o banco vazio
            }

            if (antigas == null || antigas.Count == 0)
                return;

            var servicoTemporario = new DicionarioServico { Palavras = antigas };
            servicoTemporario.Salvar();

            // Guarda o .json antigo como backup em vez de apagar, só por segurança.
            try
            {
                File.Move(ArquivoJsonAntigo, ArquivoJsonAntigo + ".bak", overwrite: true);
            }
            catch
            {
                // se não conseguir renomear, sem problema — a migração já foi feita no banco
            }
        }

        // ── Serialização dos campos de lista (guardados como JSON dentro de uma coluna TEXT) ──

        private static string SerializarLista(List<string> lista) => JsonSerializer.Serialize(lista);

        private static List<string> DesserializarLista(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
            catch { return new(); }
        }

        private static string SerializarSentidos(List<SentidoPalavra> lista) => JsonSerializer.Serialize(lista);

        private static List<SentidoPalavra> DesserializarSentidos(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SentidoPalavra>();
            try { return JsonSerializer.Deserialize<List<SentidoPalavra>>(json) ?? new(); }
            catch { return new(); }
        }

        // ── API pública (mesma interface de antes) ──

        public void Carregar()
        {
            GarantirBanco();
            MigrarDoJsonSeNecessario();

            var lista = new List<Palavra>();

            using var conexao = AbrirConexao();
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = @"
                SELECT PalavraTexto, Significado, Exemplo, Traducao, IdeiaPrincipal,
                       SignificadosJson, ExemplosJson, ExpressoesJson, SentidosJson, Dica
                FROM Palavras
                ORDER BY Id;";

            using var leitor = cmd.ExecuteReader();
            while (leitor.Read())
            {
                lista.Add(new Palavra
                {
                    PalavraTexto   = leitor.IsDBNull(0) ? "" : leitor.GetString(0),
                    Significado    = leitor.IsDBNull(1) ? "" : leitor.GetString(1),
                    Exemplo        = leitor.IsDBNull(2) ? "" : leitor.GetString(2),
                    Traducao       = leitor.IsDBNull(3) ? null : leitor.GetString(3),
                    IdeiaPrincipal = leitor.IsDBNull(4) ? null : leitor.GetString(4),
                    Significados   = DesserializarLista(leitor.IsDBNull(5) ? null : leitor.GetString(5)),
                    Exemplos       = DesserializarLista(leitor.IsDBNull(6) ? null : leitor.GetString(6)),
                    Expressoes     = DesserializarLista(leitor.IsDBNull(7) ? null : leitor.GetString(7)),
                    Sentidos       = DesserializarSentidos(leitor.IsDBNull(8) ? null : leitor.GetString(8)),
                    Dica           = leitor.IsDBNull(9) ? null : leitor.GetString(9),
                });
            }

            Palavras = lista;
        }

        /// <summary>
        /// Regrava a tabela inteira a partir da lista "Palavras" em memória (apaga tudo e
        /// insere de novo, dentro de uma transação). É o mesmo comportamento "salva tudo de
        /// uma vez" que o antigo JsonSerializer.Serialize(Palavras) fazia.
        /// </summary>
        public void Salvar()
        {
            using var conexao = AbrirConexao();
            using var transacao = conexao.BeginTransaction();

            using (var limpar = conexao.CreateCommand())
            {
                limpar.Transaction = transacao;
                limpar.CommandText = "DELETE FROM Palavras;";
                limpar.ExecuteNonQuery();
            }

            using (var inserir = conexao.CreateCommand())
            {
                inserir.Transaction = transacao;
                inserir.CommandText = @"
                    INSERT INTO Palavras
                        (PalavraTexto, Significado, Exemplo, Traducao, IdeiaPrincipal,
                         SignificadosJson, ExemplosJson, ExpressoesJson, SentidosJson, Dica)
                    VALUES
                        ($palavra, $significado, $exemplo, $traducao, $ideia,
                         $significados, $exemplos, $expressoes, $sentidos, $dica);";

                var pPalavra      = inserir.Parameters.Add("$palavra", SqliteType.Text);
                var pSignificado  = inserir.Parameters.Add("$significado", SqliteType.Text);
                var pExemplo      = inserir.Parameters.Add("$exemplo", SqliteType.Text);
                var pTraducao     = inserir.Parameters.Add("$traducao", SqliteType.Text);
                var pIdeia        = inserir.Parameters.Add("$ideia", SqliteType.Text);
                var pSignificados = inserir.Parameters.Add("$significados", SqliteType.Text);
                var pExemplos     = inserir.Parameters.Add("$exemplos", SqliteType.Text);
                var pExpressoes   = inserir.Parameters.Add("$expressoes", SqliteType.Text);
                var pSentidos     = inserir.Parameters.Add("$sentidos", SqliteType.Text);
                var pDica         = inserir.Parameters.Add("$dica", SqliteType.Text);

                foreach (var p in Palavras)
                {
                    pPalavra.Value      = p.PalavraTexto;
                    pSignificado.Value  = p.Significado;
                    pExemplo.Value      = p.Exemplo;
                    pTraducao.Value     = (object?)p.Traducao ?? DBNull.Value;
                    pIdeia.Value        = (object?)p.IdeiaPrincipal ?? DBNull.Value;
                    pSignificados.Value = SerializarLista(p.Significados);
                    pExemplos.Value     = SerializarLista(p.Exemplos);
                    pExpressoes.Value   = SerializarLista(p.Expressoes);
                    pSentidos.Value     = SerializarSentidos(p.Sentidos);
                    pDica.Value         = (object?)p.Dica ?? DBNull.Value;

                    inserir.ExecuteNonQuery();
                }
            }

            transacao.Commit();
        }

        /// <summary>
        /// Verifica se já existe uma palavra cadastrada com o mesmo texto
        /// (ignorando maiúsculas/minúsculas e espaços nas pontas).
        /// Usado para exibir o aviso de duplicidade antes de adicionar.
        /// </summary>
        public Palavra? BuscarExata(string palavra)
        {
            var termo = palavra.Trim().ToLower();
            return Palavras.Find(p => p.PalavraTexto.Trim().ToLower() == termo);
        }

        public void Adicionar(string palavra, string significado, string exemplo)
        {
            Palavras.Add(new Palavra
            {
                PalavraTexto = palavra,
                Significado  = significado,
                Exemplo      = exemplo
            });
            Salvar();
        }

        /// <summary>Adiciona uma palavra já pronta (usado pelo fluxo "Gerar Palavra").</summary>
        public void Adicionar(Palavra palavra)
        {
            Palavras.Add(palavra);
            Salvar();
        }

        public void Deletar(string palavra)
        {
            Palavras.RemoveAll(p =>
                p.PalavraTexto.ToLower() == palavra.ToLower());
            Salvar();
        }

        public List<Palavra> Buscar(string termo)
        {
            // IMPORTANTE: sempre devolve uma cópia nova da lista, nunca a lista viva
            // (Palavras) diretamente. Essa lista fica ligada ao ItemsSource da tela;
            // se devolvêssemos a referência original, toda vez que uma palavra fosse
            // adicionada/removida (Palavras.Add/RemoveAll) a mesma lista já exibida na
            // tela seria alterada por baixo dos panos, sem passar pela notificação do
            // WPF — foi isso que causava o erro "ItemsControl is inconsistent with
            // its items source".
            if (string.IsNullOrWhiteSpace(termo))
                return new List<Palavra>(Palavras);

            return Palavras.FindAll(p =>
                p.PalavraTexto.ToLower().Contains(termo.ToLower()) ||
                p.Significado .ToLower().Contains(termo.ToLower()));
        }
    }
}
