using SqlServerWorkspace.Commands;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace.ViewModels
{
	public class NameViewModel : INotifyPropertyChanged
	{
		private string _nameText = string.Empty;
		public string NameText
		{
			get => _nameText;
			set
			{
				_nameText = value;
				OnPropertyChanged();
			}
		}

		public ICommand OkCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand LoadedCommand { get; }

		public NameViewModel()
		{
			OkCommand = new RelayCommand(_ => CloseWindow(true));
			CancelCommand = new RelayCommand(_ => CloseWindow(false));
			LoadedCommand = new RelayCommand(LoadedExecute);
		}

		private void CloseWindow(bool? dialogResult)
		{
			var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
			if (window != null)
			{
				window.DialogResult = dialogResult;
				window.Close();
			}
		}

		public void LoadedExecute(object parameter)
		{
			if (parameter is TextBox textBox)
			{
				//textBox.Text = NameText;
				textBox.Focus();
				textBox.SelectAll();
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
