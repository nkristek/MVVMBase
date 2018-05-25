﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nkristek.MVVMBase.ViewModels;

namespace nkristek.MVVMBase.Tests.ViewModels
{
    /// <summary>
    /// Summary description for ViewModelTests
    /// </summary>
    [TestClass]
    public class ViewModelTests
    {
        private class TestViewModel
            : ViewModel
        {
            private bool _testProperty;
            public bool TestProperty
            {
                get => _testProperty;
                set => SetProperty(ref _testProperty, value, out _);
            }

            private TestChildViewModel _child;
            public TestChildViewModel Child
            {
                get => _child;
                set
                {
                    if (SetProperty(ref _child, value, out var oldValue))
                    {
                        if (oldValue != null)
                            RemoveChildViewModel(oldValue);
                        if (value != null)
                            AddChildViewModel(value);
                    }
                }
            }

            private ObservableCollection<int> _values;
            public ObservableCollection<int> Values
            {
                get => _values;
                set => SetProperty(ref _values, value, out _);
            }
        }

        private class TestChildViewModel
            : ViewModel
        {
            private bool _anotherTestProperty;
            public bool AnotherTestProperty
            {
                get => _anotherTestProperty;
                set => SetProperty(ref _anotherTestProperty, value, out _);
            }
        }

        [TestMethod]
        public void TestCollectionChangedIsDirty()
        {
            var valueCollection = new ObservableCollection<int>();
            var viewModel = new TestViewModel
            {
                Values = valueCollection,
                IsDirty = false
            };
            Assert.IsFalse(viewModel.IsDirty, "ViewModel.IsDirty is true after init");

            valueCollection.Add(1);
            Assert.IsTrue(viewModel.IsDirty, "ViewModel.IsDirty is false after changing the collection");
        }

        [TestMethod]
        public void TestIsDirty()
        {
            var viewModel = new TestViewModel();
            Assert.IsFalse(viewModel.IsDirty, "IsDirty is not initially false");
            viewModel.TestProperty = true;
            Assert.IsTrue(viewModel.IsDirty, "IsDirty wasn't set by the test property");
            
            viewModel.Child = new TestChildViewModel();
            viewModel.IsDirty = false;
            Assert.IsFalse(viewModel.IsDirty, "IsDirty was not reset");
            
            viewModel.Child.AnotherTestProperty = true;
            Assert.IsTrue(viewModel.IsDirty, "IsDirty wasn't set by the child viewmodel property");
        }

        [TestMethod]
        public void TestParent()
        {
            var viewModel = new TestViewModel();
            var childViewModel = new TestChildViewModel
            {
                Parent = viewModel
            };
            Assert.IsNotNull(childViewModel.Parent, "Parent was not set");
            childViewModel.Parent = viewModel;
            Assert.IsNotNull(childViewModel.Parent, "Parent was not set");
        }

        [TestMethod]
        public void TestIsReadonly()
        {
            var viewModel = new TestViewModel
            {
                IsReadOnly = true
            };
            Assert.IsTrue(viewModel.IsReadOnly, "IsReadOnly wasn't set");
            viewModel.TestProperty = true;
            Assert.IsFalse(viewModel.TestProperty, "TestProperty was set although the viewmodel was readonly");
        }

        [TestMethod]
        public void TestChildViewModelPropertyChanged()
        {
            var invokedPropertyChangedEvents = new List<string>();

            var viewModel = new TestViewModel
            {
                Child = new TestChildViewModel()
            };

            viewModel.PropertyChanged += (sender, e) =>
            {
                invokedPropertyChangedEvents.Add(e.PropertyName);
            };

            viewModel.Child.AnotherTestProperty = true;

            Assert.AreEqual(1, invokedPropertyChangedEvents.Count, "Invalid count of invocations of the PropertyChanged event");
            Assert.IsTrue(invokedPropertyChangedEvents.Contains(nameof(TestViewModel.Child)), "The PropertyChanged event wasn't raised for the childviewmodel property");

            viewModel.Child = null;
            Assert.AreEqual(2, invokedPropertyChangedEvents.Count, "Invalid count of invocations of the PropertyChanged event");
        }
    }
}
