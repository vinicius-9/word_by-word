using System;
using System.Windows;

namespace WordByWord
{
    public partial class App : Application
    {
        /// <summary>Indica se o tema claro está ativo no momento.</summary>
        public static bool TemaClaroAtivo { get; private set; }

        /// <summary>Disparado sempre que o tema é trocado, para as janelas atualizarem ícones/textos.</summary>
        public static event EventHandler? TemaAlterado;

        // Evita empilhar vários diálogos de erro um em cima do outro 
        private static bool _mostrandoErroFatal;
        private static int _totalErrosSeguidos;
        private static DateTime _ultimoErro = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Rede de segurança: se algum erro não previsto acontecer em qualquer parte do
            // app (ex.: falha ao gravar um arquivo, erro de rede inesperado), mostra uma
            // mensagem explicando em vez de travar ou fechar o app sem explicação nenhuma.
            DispatcherUnhandledException += (_, args) =>
            {
                // Se já tem um diálogo de erro na tela, não abre outro por cima —
                // só marca como tratado e deixa o primeiro diálogo em paz.
                if (_mostrandoErroFatal)
                {
                    args.Handled = true;
                    return;
                }

                // Se o mesmo tipo de erro voltar a acontecer muitas vezes em poucos segundos,
                // é sinal de um problema que vai se repetir pra sempre (ex.: loop de layout).
                // Nesse caso é mais seguro deixar o app fechar do que ficar tentando continuar.
                var agora = DateTime.Now;
                _totalErrosSeguidos = (agora - _ultimoErro).TotalSeconds < 2 ? _totalErrosSeguidos + 1 : 1;
                _ultimoErro = agora;

                if (_totalErrosSeguidos > 3)
                {
                    MessageBox.Show(
                        $"O app encontrou um erro que continua se repetindo e vai precisar fechar:\n\n" +
                        $"{args.Exception.Message}",
                        "Word By Word — Erro",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return; // args.Handled continua false → o app fecha, sem novos diálogos.
                }

                _mostrandoErroFatal = true;
                try
                {
                    MessageBox.Show(
                        $"Ocorreu um erro inesperado:\n\n{args.Exception.Message}\n\nO app vai continuar aberto.",
                        "Word By Word — Erro",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                finally
                {
                    _mostrandoErroFatal = false;
                }
                args.Handled = true;
            };

            // Sempre inicia no tema claro — o usuário pode alternar para o escuro
            // durante o uso, mas cada abertura do app começa no tema claro.
            AplicarTema(claro: true);
        }

        /// <summary>Alterna entre tema claro e escuro (vale só para esta sessão; a próxima
        /// abertura do app sempre volta a começar no tema claro).</summary>
        public static void AlternarTema()
        {
            AplicarTema(!TemaClaroAtivo);
        }

        private static void AplicarTema(bool claro)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(
                    claro ? "Themes/TemaClaro.xaml" : "Themes/TemaEscuro.xaml",
                    UriKind.Relative)
            };

            var recursos = Current.Resources.MergedDictionaries;
            recursos.Clear();
            recursos.Add(dict);

            TemaClaroAtivo = claro;

            TemaAlterado?.Invoke(null, EventArgs.Empty);
        }
    }
}
