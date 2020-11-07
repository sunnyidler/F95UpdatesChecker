using System.Windows;
using System.Windows.Input;

namespace F95UpdatesChecker
{
    public static class F95LoginCommands
    {
        #region Public fields

        public static RoutedCommand LoginCommand = new RoutedCommand(nameof(LoginCommand), typeof(F95LoginCommands));

        public static RoutedCommand ReloginCommand = new RoutedCommand(nameof(ReloginCommand), typeof(F95LoginCommands));

        #endregion
    }

    /// <summary>
    /// Interaction logic for F95LoginWIndow.xaml
    /// </summary>
    public partial class F95LoginWIndow : Window
    {
        #region Private fields

        private readonly F95LoginViewModel viewModel;

        #endregion

        #region Constructors

        public F95LoginWIndow()
        {
            InitializeComponent();
        }

        public F95LoginWIndow(F95LoginViewModel viewModel) : this()
        {
            this.viewModel = viewModel;
            DataContext = this.viewModel;

            passwordBox.Password = viewModel.Password;

            CommandBindings.Add(new CommandBinding(F95LoginCommands.LoginCommand,
                async (object sender, ExecutedRoutedEventArgs e) =>
                {
                    var loginResult = await viewModel.LoginAsync();
                    if (loginResult)
                        Close();                      
                },
                (object sender, CanExecuteRoutedEventArgs e) =>
                {
                    e.CanExecute = !string.IsNullOrWhiteSpace(viewModel.Username) && !string.IsNullOrWhiteSpace(viewModel.Password);
                }));
        }

        #endregion

        #region Private methods

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            viewModel.Password = passwordBox.Password;
        }

        #endregion
    }
}
