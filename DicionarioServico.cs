using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace WordByWord
{
    /// <summary>
    /// Guarda as palavras cadastradas em um banco SQLite local (dicionario.db).
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

        /// <summary>Migra o dicionario.json antigo pro banco, uma única vez, se ele existir.</summary>
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
                    return;
            }

            List<Palavra>? antigas;
            try
            {
                var json = File.ReadAllText(ArquivoJsonAntigo);
                antigas = JsonSerializer.Deserialize<List<Palavra>>(json);
            }
            catch
            {
                return;
            }

            if (antigas == null || antigas.Count == 0)
                return;

            var servicoTemporario = new DicionarioServico { Palavras = antigas };
            servicoTemporario.Salvar();

            try
            {
                File.Move(ArquivoJsonAntigo, ArquivoJsonAntigo + ".bak", overwrite: true);
            }
            catch
            {
                // migração já concluída no banco; renomear o .json é só cosmético
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

        /// <summary>Apaga e regrava a tabela inteira a partir da lista "Palavras" em memória.</summary>
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

        /// <summary>Busca uma palavra já cadastrada com o mesmo texto (ignora maiúsculas/espaços).</summary>
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

        public void Deletar(string palavra)
        {
            Palavras.RemoveAll(p =>
                p.PalavraTexto.ToLower() == palavra.ToLower());
            Salvar();
        }

        public List<Palavra> Buscar(string termo)
        {
            // Sempre devolve uma cópia nova, nunca a lista "Palavras" viva — evita
            // o erro "ItemsControl is inconsistent with its items source" no WPF.
            if (string.IsNullOrWhiteSpace(termo))
                return new List<Palavra>(Palavras);

            return Palavras.FindAll(p =>
                p.PalavraTexto.ToLower().Contains(termo.ToLower()) ||
                p.Significado .ToLower().Contains(termo.ToLower()));
        }
    }
}
