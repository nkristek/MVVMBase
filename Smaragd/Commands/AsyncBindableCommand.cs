﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using NKristek.Smaragd.ViewModels;

namespace NKristek.Smaragd.Commands
{
    /// <summary>
    /// Asynchronous <see cref="ICommand"/> with <see cref="INotifyPropertyChanged"/> support
    /// </summary>
    public abstract class AsyncBindableCommand
        : ComputedBindableBase, IAsyncCommand, IRaiseCanExecuteChanged
    {
        private bool _isWorking;

        /// <inheritdoc />
        public bool IsWorking
        {
            get => _isWorking;
            private set
            {
                if (SetProperty(ref _isWorking, value, out _))
                    RaiseCanExecuteChanged();
            }
        }

        /// <inheritdoc />
        public virtual bool CanExecute(object parameter)
        {
            return !IsWorking;
        }

        /// <inheritdoc />
        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(object parameter)
        {
            try
            {
                IsWorking = true;
                await DoExecute(parameter);
            }
            finally
            {
                IsWorking = false;
            }
        }

        /// <summary>
        /// Asynchronous <see cref="ICommand.Execute(object)"/>
        /// </summary>
        /// <param name="parameter">Optional parameter</param>
        /// <returns>The <see cref="Task"/> of this execution</returns>
        protected abstract Task DoExecute(object parameter);
        
        /// <inheritdoc />
        public virtual event EventHandler CanExecuteChanged;

        /// <inheritdoc />
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
