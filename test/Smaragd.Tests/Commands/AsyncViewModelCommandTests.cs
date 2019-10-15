﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NKristek.Smaragd.Attributes;
using NKristek.Smaragd.Commands;
using NKristek.Smaragd.ViewModels;
using Xunit;

namespace NKristek.Smaragd.Tests.Commands
{
    public class AsyncViewModelCommandTests
    {
        private class TestViewModel
            : ViewModel
        {
            private bool _testProperty;

            public bool TestProperty
            {
                get => _testProperty;
                set => SetProperty(ref _testProperty, value);
            }
        }

        private class AsyncRelayViewModelCommand
            : AsyncViewModelCommand<TestViewModel>
        {
            private readonly Func<TestViewModel?, object?, Task> _execute;

            private readonly Func<TestViewModel?, object?, bool>? _canExecute;

            public AsyncRelayViewModelCommand(Func<TestViewModel?, object?, Task> execute,
                Func<TestViewModel?, object?, bool>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            protected override bool CanExecute(TestViewModel? viewModel, object? parameter)
            {
                return _canExecute?.Invoke(viewModel, parameter) ?? base.CanExecute(viewModel, parameter);
            }

            protected override async Task ExecuteAsync(TestViewModel? viewModel, object? parameter)
            {
                await _execute.Invoke(viewModel, parameter);
            }
        }

        private class AsyncConcurrentRelayViewModelCommand
            : AsyncViewModelCommand<TestViewModel>
        {
            private readonly Func<TestViewModel?, object?, Task> _execute;

            private readonly Func<TestViewModel?, object?, bool>? _canExecute;

            /// <inheritdoc />
            public override bool AllowsConcurrentExecution => true;

            public AsyncConcurrentRelayViewModelCommand(Func<TestViewModel?, object?, Task> execute,
                Func<TestViewModel?, object?, bool>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            protected override bool CanExecute(TestViewModel? viewModel, object? parameter)
            {
                return _canExecute?.Invoke(viewModel, parameter) ?? base.CanExecute(viewModel, parameter);
            }

            protected override async Task ExecuteAsync(TestViewModel? viewModel, object? parameter)
            {
                await _execute.Invoke(viewModel, parameter);
            }
        }

        private class DefaultAsyncViewModelCommand
            : AsyncViewModelCommand<TestViewModel>
        {
            protected override async Task ExecuteAsync(TestViewModel? viewModel, object? parameter)
            {
                await Task.Run(() =>
                {
                    if (viewModel == null)
                        throw new ArgumentNullException(nameof(viewModel));

                    if (parameter == null)
                        throw new ArgumentNullException(nameof(parameter));

                    if (viewModel != parameter)
                        throw new Exception("invalid parameter");
                });
            }

            public void NotifyCanExecuteChangedExternal()
            {
                NotifyCanExecuteChanged();
            }
        }

        private class CanExecuteSourceAsyncViewModelCommand
            : AsyncViewModelCommand<TestViewModel>
        {
            protected override bool CanExecute(TestViewModel? viewModel, object? parameter)
            {
                return viewModel?.TestProperty ?? false;
            }

            /// <inheritdoc />
            protected override void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs? e)
            {
                if (e == null || String.IsNullOrEmpty(e.PropertyName) || e.PropertyName.Equals(nameof(TestViewModel.TestProperty)))
                    NotifyCanExecuteChanged();
            }

            protected override Task ExecuteAsync(TestViewModel? viewModel, object? parameter)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void IsWorking_is_initially_false()
        {
            var command = new DefaultAsyncViewModelCommand();
            Assert.False(command.IsWorking);
        }

        [Fact]
        public void CanExecute_returns_true_by_default()
        {
            var command = new DefaultAsyncViewModelCommand();
            Assert.True(command.CanExecute(null));
        }

        [Fact]
        public void Context_disposed_is_null()
        {
            var command = CreateTestCommandWithDisposedContext();
            GCHelper.TriggerGC();
            Assert.Null(command.Context);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static DefaultAsyncViewModelCommand CreateTestCommandWithDisposedContext()
        {
            var viewModel = new TestViewModel();
            return new DefaultAsyncViewModelCommand
            {
                Context = viewModel
            };
        }

        [Fact]
        public async Task CanExecute_returns_false_if_command_is_working()
        {
            var lockObject = new object();
            var command = new AsyncRelayViewModelCommand(async (vm, para) => await Task.Run(() =>
            {
                lock (lockObject)
                {
                }
            }));

            Task executeTask;
            lock (lockObject)
            {
                executeTask = command.ExecuteAsync(null);
                Assert.False(command.CanExecute(null));
            }

            await executeTask;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void CanExecute_With_ViewModel_Is_Evaluated(bool input, bool expectedResult)
        {
            var command = new AsyncRelayViewModelCommand(async (vm, para) => await Task.Run(() => { }),
                (vm, para) => para is bool boolPara && boolPara);
            Assert.Equal(expectedResult, command.CanExecute(input));
        }

        [Fact]
        public void CanExecute_ViewModel_NotNull()
        {
            var canExecuteWasExecuted = false;
            var viewModel = new TestViewModel();
            var command = new AsyncRelayViewModelCommand(async (vm, para) => await Task.Run(() => { }), (vm, para) =>
            {
                if (vm == null)
                    throw new ArgumentNullException(nameof(vm));
                if (vm != viewModel)
                    throw new ArgumentException(nameof(vm));
                canExecuteWasExecuted = true;
                return true;
            })
            {
                Context = viewModel
            };
            command.CanExecute(null);
            Assert.True(canExecuteWasExecuted);
        }

        [Fact]
        public async Task ICommandExecute_executes_ExecuteAsync()
        {
            var executeWasExecuted = false;
            var task = new Task(() => executeWasExecuted = true);
            var command = new AsyncRelayViewModelCommand(async (vm, para) =>
            {
                task.Start();
                await task;
            });
            ((ICommand) command).Execute(null);
            
            if (await Task.WhenAny(task, Task.Delay(1000)) == task)
            {
                // task completed within timeout, await task because it may have faulted
                await task;
            }
            else
            {
                // timeout logic
                throw new Exception("Timeout");
            }

            Assert.True(executeWasExecuted);
        }

        [Fact]
        public async Task ExecuteAsync_executes_ExecuteAsync_with_ViewModel()
        {
            var executeWasExecuted = false;
            var command = new AsyncRelayViewModelCommand(async (vm, para) => await Task.Run(() => executeWasExecuted = true));
            await command.ExecuteAsync(null);
            Assert.True(executeWasExecuted);
        }

        [Fact]
        public async Task ExecuteAsync_sets_IsWorking()
        {
            var lockObject = new object();
            var command = new AsyncRelayViewModelCommand(async (vm, para) => await Task.Run(() =>
            {
                lock (lockObject)
                {
                }
            }));

            Task executeTask;
            lock (lockObject)
            {
                executeTask = command.ExecuteAsync(null);
                Assert.True(command.IsWorking);
            }

            await executeTask;
            Assert.False(command.IsWorking);
        }

        [Fact]
        public async Task ExecuteAsync_ViewModel_NotNull()
        {
            var viewModel = new TestViewModel();
            var command = new AsyncRelayViewModelCommand(async (vm, para) =>
            {
                await Task.Run(() =>
                {
                    if (vm == null)
                        throw new ArgumentNullException(nameof(vm));
                    if (vm != viewModel)
                        throw new ArgumentException(nameof(vm));
                });
            })
            {
                Context = viewModel
            };
            await command.ExecuteAsync(null);
        }

        [Fact]
        public async Task ExecuteAsync_Parameter_NotNull()
        {
            var viewModel = new TestViewModel();
            var command = new AsyncRelayViewModelCommand(async (vm, para) =>
            {
                await Task.Run(() =>
                {
                    if (para == null)
                        throw new ArgumentNullException(nameof(para));
                    if (para != viewModel)
                        throw new ArgumentException(nameof(para));
                });
            });
            await command.ExecuteAsync(viewModel);
        }

        [Fact]
        public void NotifyCanExecuteChanged_raises_event_on_CanExecuteChanged()
        {
            var invokedCanExecuteChangedEvents = 0;
            var command = new DefaultAsyncViewModelCommand();
            command.NotifyCanExecuteChangedExternal();
            command.CanExecuteChanged += (sender, args) => invokedCanExecuteChangedEvents++;
            command.NotifyCanExecuteChangedExternal();
            Assert.Equal(1, invokedCanExecuteChangedEvents);
        }

        [Fact]
        public void ContextPropertyChanged_raises_event_on_CanExecuteChanged()
        {
            var viewModel = new TestViewModel();
            var command = new CanExecuteSourceAsyncViewModelCommand
            {
                Context = viewModel
            };

            var invokedCanExecuteChangedEvents = 0;
            command.CanExecuteChanged += (sender, args) => invokedCanExecuteChangedEvents++;
            viewModel.TestProperty = true;
            Assert.Equal(1, invokedCanExecuteChangedEvents);
        }

        [Fact]
        public void Context_set()
        {
            var viewModel = new TestViewModel();
            var command = new DefaultAsyncViewModelCommand
            {
                Context = viewModel
            };
            Assert.Equal(viewModel, command.Context);
        }

        [Fact]
        public void Context_set_twice()
        {
            var viewModel = new TestViewModel();
            var command = new DefaultAsyncViewModelCommand
            {
                Context = viewModel
            };
            command.Context = viewModel;
            Assert.Equal(viewModel, command.Context);
        }

        [Fact]
        public void Context_set_after_set()
        {
            var firstViewModel = new TestViewModel();
            var secondViewModel = new TestViewModel();
            var command = new DefaultAsyncViewModelCommand
            {
                Context = firstViewModel
            };
            command.Context = secondViewModel;
            Assert.Equal(secondViewModel, command.Context);
        }

        [Fact]
        public void Context_set_to_null()
        {
            var viewModel = new TestViewModel();
            var command = new DefaultAsyncViewModelCommand
            {
                Context = viewModel
            };

            command.Context = null;
            Assert.Null(command.Context);
        }

        [Fact]
        public async Task AllowsConcurrentExecution_true()
        {
            using var mainSemaphore = new SemaphoreSlim(0);
            using var commandSemaphore = new SemaphoreSlim(0);
            var actualExecutionCount = 0;

            var command = new AsyncConcurrentRelayViewModelCommand(async (vm, para) =>
            {
                Interlocked.Increment(ref actualExecutionCount);
                mainSemaphore.Release();
                await commandSemaphore.WaitAsync();
            });
            // start executing the first command
            var firstTask = command.ExecuteAsync(null);
            // wait until the first command is really executing
            await mainSemaphore.WaitAsync();
            // start executing the second command
            var secondTask = command.ExecuteAsync(null);
            // release 2 times to firstly unlock the first command. The second release should not be necessary if the second command didn't execute (as expected), but to avoid deadlocks it should be done.
            commandSemaphore.Release(2);
            await Task.WhenAll(firstTask, secondTask);
            Assert.Equal(2, actualExecutionCount);
        }

        [Fact]
        public async Task AllowsConcurrentExecution_false()
        {
            using var mainSemaphore = new SemaphoreSlim(0);
            using var commandSemaphore = new SemaphoreSlim(0);
            var actualExecutionCount = 0;

            var command = new AsyncRelayViewModelCommand(async (vm, para) =>
            {
                Interlocked.Increment(ref actualExecutionCount);
                mainSemaphore.Release();
                await commandSemaphore.WaitAsync();
            });
            // start executing the first command
            var firstTask = command.ExecuteAsync(null);
            // wait until the first command is really executing
            await mainSemaphore.WaitAsync(); 
            // start executing the second command
            var secondTask = command.ExecuteAsync(null);
            // release 2 times to firstly unlock the first command. The second release should not be necessary if the second command didn't execute (as expected), but to avoid deadlocks it should be done.
            commandSemaphore.Release(2);
            await Task.WhenAll(firstTask, secondTask);
            Assert.Equal(1, actualExecutionCount);
        }

        [Fact]
        public async Task ExecuteAsync_when_CanExecute_false()
        {
            var didExecute = false;
            var command = new AsyncRelayViewModelCommand(async (viewModel, parameter) =>
            {
                didExecute = true;
                await Task.Yield();
            }, (viewModel, parameter) => false);
            await command.ExecuteAsync(null);
            Assert.False(didExecute);
        }
    }
}