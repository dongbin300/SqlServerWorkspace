using System.Windows.Input;

namespace SqlServerWorkspace.Commands
{
	public class RelayCommand : ICommand
	{
		private readonly Action<object> _execute;
		private readonly Func<object, bool> _canExecute;

		public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		public RelayCommand(Action<object> execute)
		{
			_execute = execute;
			_canExecute = default!;
		}

		public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter!) ?? true;
		public void Execute(object? parameter) => _execute(parameter ?? throw new ArgumentNullException(nameof(parameter)));
		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}
	}
}
