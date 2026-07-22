using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WordByWord
{
    /// <summary>
    /// Propriedade anexada que aplica uma formatação leve (parecida com markdown)
    /// ao texto de um TextBlock: **negrito**, listas com "- " e parágrafos
    /// separados por linha em branco viram um layout organizado, em vez de
    /// aparecer tudo em um bloco só de texto corrido com os símbolos literais.
    ///
    /// Uso no XAML (em vez de Text="{Binding Texto}"):
    ///   local:FormatadorTexto.Texto="{Binding Texto}"
    /// </summary>
    public static class FormatadorTexto
    {
        public static readonly DependencyProperty TextoProperty =
            DependencyProperty.RegisterAttached(
                "Texto",
                typeof(string),
                typeof(FormatadorTexto),
                new PropertyMetadata(null, AoMudarTexto));

        public static string GetTexto(DependencyObject obj) => (string)obj.GetValue(TextoProperty);
        public static void SetTexto(DependencyObject obj, string value) => obj.SetValue(TextoProperty, value);

        private static readonly Regex RegexNegrito = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        // Linha que é só um separador de markdown (---, ----------, ___, ***) — não deve
        // aparecer pro usuário, é só "ruído" que às vezes a IA ainda inclui na resposta.
        private static readonly Regex RegexLinhaSeparadora = new(@"^\s*([-_*])\1{2,}\s*$", RegexOptions.Compiled);

        // Linha que começa com "#", "##" ou "###" (título estilo markdown). Em vez de mostrar
        // os símbolos literais, tratamos o resto da linha como um pequeno destaque em negrito.
        private static readonly Regex RegexTitulo = new(@"^\s{0,3}#{1,6}\s*(.*)$", RegexOptions.Compiled);

        private static void AoMudarTexto(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock tb) return;

            tb.Inlines.Clear();
            var texto = e.NewValue as string;
            if (string.IsNullOrEmpty(texto)) return;

            // Remove linhas que são só separadores de markdown ("---", "___", etc.) antes de
            // quebrar em parágrafos, senão cada uma delas some sozinha mas ainda deixa um
            // espaçamento extra estranho entre as seções.
            var linhasBrutas = texto.Replace("\r\n", "\n").Split('\n')
                .Where(l => !RegexLinhaSeparadora.IsMatch(l));
            var normalizado = string.Join("\n", linhasBrutas).Trim();

            var paragrafos = Regex.Split(normalizado, @"\n\s*\n");

            for (int p = 0; p < paragrafos.Length; p++)
            {
                var paragrafo = paragrafos[p].Trim();
                if (paragrafo.Length == 0) continue;

                var linhas = paragrafo.Split('\n');
                for (int i = 0; i < linhas.Length; i++)
                {
                    var linha = linhas[i].TrimEnd();
                    var linhaSemEspaco = linha.TrimStart();

                    var matchTitulo = RegexTitulo.Match(linhaSemEspaco);
                    bool ehItemDeLista = linhaSemEspaco.StartsWith("- ") || linhaSemEspaco.StartsWith("* ");

                    if (matchTitulo.Success)
                    {
                        // "### Expressões comuns" → só "Expressões comuns", em negrito.
                        tb.Inlines.Add(new Bold(new Run(matchTitulo.Groups[1].Value.Trim())));
                    }
                    else if (ehItemDeLista)
                    {
                        tb.Inlines.Add(new Run("•  "));
                        AdicionarComNegrito(tb, linhaSemEspaco[2..].Trim());
                    }
                    else
                    {
                        AdicionarComNegrito(tb, linha);
                    }

                    if (i < linhas.Length - 1)
                        tb.Inlines.Add(new LineBreak());
                }

                if (p < paragrafos.Length - 1)
                {
                    tb.Inlines.Add(new LineBreak());
                    tb.Inlines.Add(new LineBreak());
                }
            }
        }

        /// <summary>Adiciona o texto de uma linha destacando trechos **em negrito**.</summary>
        private static void AdicionarComNegrito(TextBlock tb, string linha)
        {
            int cursor = 0;
            foreach (Match m in RegexNegrito.Matches(linha))
            {
                if (m.Index > cursor)
                    tb.Inlines.Add(new Run(linha[cursor..m.Index]));

                tb.Inlines.Add(new Bold(new Run(m.Groups[1].Value)));
                cursor = m.Index + m.Length;
            }

            if (cursor < linha.Length)
                tb.Inlines.Add(new Run(linha[cursor..]));
        }
    }
}
