using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace.Views.CustomControls
{
	/// <summary>
	/// ClearableTextBox.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ClearableTextBox : UserControl
	{
		public ClearableTextBox()
		{
			InitializeComponent();
			TextBoxPart.TextChanged += TextBoxPart_TextChanged;
		}

		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register(nameof(Text), typeof(string), typeof(ClearableTextBox),
				new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

		public string Text
		{
			get => (string)GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ClearableTextBox clearableTextBox && clearableTextBox.TextBoxPart != null)
			{
				clearableTextBox.TextBoxPart.Text = e.NewValue?.ToString();
			}
		}

		private void TextBoxPart_TextChanged(object sender, TextChangedEventArgs e)
		{
			ClearButton.Visibility = string.IsNullOrEmpty(TextBoxPart.Text)
				? Visibility.Collapsed
				: Visibility.Visible;

			Text = TextBoxPart.Text;
		}

		private void ClearButton_MouseDown(object sender, MouseButtonEventArgs e)
		{
			TextBoxPart.Clear();
		}
	}
}
