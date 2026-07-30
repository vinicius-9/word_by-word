using System;
using System.Windows;

namespace WordByWord
{
    public partial class App : Application
    {
        public static bool TemaClaroAtivo { get; private set; }

        /// <summary>Disparado sempre que o tema é trocado.</summary>
        public static event EventHandler? TemaAlterado;

        // Evita empilhar vários diálogos de erro um em cima do outro.
        private static bool _mostrandoErroFatal;
        private static int _totalErrosSeguidos;
        private static DateTime _ultimoErro = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Mostra uma mensagem em erros não previstos, em vez de fechar o app sem explicação.
            DispatcherUnhandledException += (_, args) =>
            {
                // Já tem um diálogo de erro na tela: não abre outro por cima.
                if (_mostrandoErroFatal)
                {
                    args.Handled = true;
                    return;
                }

                // Erro se repetindo muitas vezes em poucos segundos: mais seguro deixar o app fechar.
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
                    return; // args.Handled continua false → o app fecha
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

            // Sempre inicia no tema claro; o usuário pode alternar durante o uso.
            AplicarTema(claro: true);
        }

        /// <summary>Alterna entre tema claro e escuro (só nesta sessão; a próxima abertura volta ao claro).</summary>
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
