using System.Windows;

namespace AdbInstallerApp.Views
{
    public partial class InputDialog : Window
    {
        public string Question { get; set; }
        public string Answer { get; set; }

        public InputDialog(string title, string question, string defaultAnswer = "")
        {
            InitializeComponent();
            Title = title;
            Question = question;
            Answer = defaultAnswer;
            DataContext = this;
            
            AnswerTextBox.Focus();
            AnswerTextBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
