using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WordByWord
{
    /// <summary>
    /// Tela de carregamento exibida rapidamente ao abrir o app, antes da janela principal.
    /// </summary>
    public partial class TelaCarregamento : Window
    {
        // Tempos da sequência de abertura (em ms)
        private const int DuracaoFadeInJanelaMs = 600;
        private const int DuracaoQuadro1Ms = 1500;

        public TelaCarregamento()
        {
            InitializeComponent();
            AplicarLogoDoTema();
            Loaded += (_, _) => IniciarAnimacoes();
        }

        /// <summary>
        /// Carrega sempre a logo clara.
        /// </summary>
        private void AplicarLogoDoTema()
        {
            imgLogo.Source = new BitmapImage(
                new Uri("pack://application:,,,/Assets/logo-clara.png", UriKind.Absolute));
        }

        private void IniciarAnimacoes()
        {
            // Fade da janela
            BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(DuracaoFadeInJanelaMs))
                {
                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                });

            // Giro do anel
            var giro = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1.1),
                RepeatBehavior = RepeatBehavior.Forever
            };

            rotacaoAnel.BeginAnimation(RotateTransform.AngleProperty, giro);

            // Pulso da logo
            var pulso = new DoubleAnimation
            {
                From = 1.0,
                To = 1.06,
                Duration = TimeSpan.FromSeconds(0.9),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            escalaLogo.BeginAnimation(ScaleTransform.ScaleXProperty, pulso);
            escalaLogo.BeginAnimation(ScaleTransform.ScaleYProperty, pulso);

            IniciarSequenciaDeLogo();
        }

        /// <summary>
        /// Mostra a primeira imagem e depois revela a logo principal.
        /// Assim que a logo terminar de aparecer, abre a janela principal.
        /// </summary>
        private void IniciarSequenciaDeLogo()
        {
            var suave = new QuadraticEase
            {
                EasingMode = EasingMode.EaseInOut
            };

            var esperaLogoInicial = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DuracaoQuadro1Ms)
            };

            esperaLogoInicial.Tick += (_, _) =>
            {
                esperaLogoInicial.Stop();

                var desaparecer = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = suave
                };

                desaparecer.Completed += (_, _) =>
                {
                    var aparecer = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
                    {
                        EasingFunction = suave
                    };

                    aparecer.Completed += (_, _) =>
                    {
                        AbrirJanelaPrincipal();
                    };

                    imgLogo.BeginAnimation(UIElement.OpacityProperty, aparecer);
                };

                imgSequencia1.BeginAnimation(UIElement.OpacityProperty, desaparecer);
            };

            esperaLogoInicial.Start();
        }

        private void AbrirJanelaPrincipal()
        {
            var principal = new MainWindow();
            Application.Current.MainWindow = principal;
            principal.Show();
            Close();
        }
    }
}