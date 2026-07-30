using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WordByWord
{
    /// <summary>
    /// Propriedade anexada que aplica formatação leve (tipo markdown) a um TextBlock:
    /// **negrito**, listas com "- " e parágrafos separados por linha em branco.
    /// Uso no XAML: local:FormatadorTexto.Texto="{Binding Texto}"
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

        // Linha que é só separador de markdown (---, ___, ***) — ruído que a IA às vezes inclui.
        private static readonly Regex RegexLinhaSeparadora = new(@"^\s*([-_*])\1{2,}\s*$", RegexOptions.Compiled);

        // Linha de título markdown ("#", "##", "###") — vira destaque em negrito, sem os símbolos.
        private static readonly Regex RegexTitulo = new(@"^\s{0,3}#{1,6}\s*(.*)$", RegexOptions.Compiled);

        private static void AoMudarTexto(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock tb) return;

            tb.Inlines.Clear();
            var texto = e.NewValue as string;
            if (string.IsNullOrEmpty(texto)) return;

            // Remove linhas separadoras antes de quebrar em parágrafos, pra não sobrar espaçamento extra.
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
